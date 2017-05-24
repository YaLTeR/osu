// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using OpenTK;
using osu.Framework.MathUtils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using System;
using System.Diagnostics;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Replays;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Replays
{
    public class OsuAutoGenerator : OsuAutoGeneratorBase
    {
        #region Parameters

        /// <summary>
        /// If delayed movements should be used, causing the cursor to stay on each hitobject for as long as possible.
        /// Mainly for Autopilot.
        /// </summary>
        public bool DelayedMovements; // ModManager.CheckActive(Mods.Relax2);

        #endregion

        #region Constants

        /// <summary>
        /// The "reaction time" in ms between "seeing" a new hit object and moving to "react" to it.
        /// </summary>
        private readonly double reactionTime;

        /// <summary>
        /// What easing to use when moving between hitobjects
        /// </summary>
        private EasingTypes preferredEasing => DelayedMovements ? EasingTypes.InOutCubic : EasingTypes.Out;

        #endregion

        #region Construction / Initialisation

        public OsuAutoGenerator(Beatmap<OsuHitObject> beatmap)
            : base(beatmap)
        {
            // Already superhuman, but still somewhat realistic
            reactionTime = ApplyModsToRate(100);

            FirstPass = new List<FirstPassFrame>();
            Spins = new List<Spin>();
        }

        #endregion

        #region Generator

        /// <summary>
        /// Which button (left or right) to use for the current hitobject.
        /// Even means LMB will be used to click, odd means RMB will be used.
        /// This keeps track of the button previously used for alt/singletap logic.
        /// </summary>
        private int buttonIndex;

        /// <summary>
        /// List of frames filled during the first pass through the beatmap.
        /// Contains frames for circles and sliders.
        /// </summary>
        private List<FirstPassFrame> FirstPass;

        /// <summary>
        /// List of time ranges where Auto should spin.
        /// Filled during the first pass through the beatmap.
        /// </summary>
        private List<Spin> Spins;

        public override Replay Generate()
        {
            buttonIndex = 0;

            AddFrameToReplay(new ReplayFrame(-100000, 256, 500, ReplayButtonState.None));
            AddFrameToReplay(new ReplayFrame(Beatmap.HitObjects[0].StartTime - 1500, 256, 500, ReplayButtonState.None));
            AddFrameToReplay(new ReplayFrame(Beatmap.HitObjects[0].StartTime - 1000, 256, 192, ReplayButtonState.None));

            // First of all, fill out the important frames for circles and sliders, as well as the Spins list.
            foreach (var h in Beatmap.HitObjects)
            {
                addImportantFrames(h);
            }

            // Now add the spin frames from the Spins list.
            foreach (var spin in Spins)
            {
                addSpinFrames(spin);
            }

            // Right now FirstPass can have multiple clicks and slider ticks per timestamp,
            // but at most one spin.

            // Resolve conflicts like multiple different slider ticks on one frame.
            resolveConflicts();

            // Right now FirstPass can have multiple clicks per timestamp,
            // but at most one hold (slider tick, spin or slider end) per timestamp, which will be satisfied after the clicks.

            for (int i = 0; i < FirstPass.Count - 1; i++)
            {
                var frame = FirstPass[i];
                var nextFrame = FirstPass[i + 1];

                AddFrameToReplay(new ReplayFrame(frame.Time, frame.Position.X, frame.Position.Y, ReplayButtonState.Left1));

                if (nextFrame.Type == FrameType.CLICK)
                {
                    var releaseTime = frame.Time;

                    // Only add KEY_UP_DELAY after clicks.
                    // Other frame types already had the button held for some time.
                    if (frame.Type == FrameType.CLICK)
                        releaseTime = Math.Min(frame.Time + KEY_UP_DELAY, nextFrame.Time);

                    AddFrameToReplay(new ReplayFrame(releaseTime, frame.Position.X, frame.Position.Y, ReplayButtonState.None));
                }
                else
                {
                    // Follow sliders.
                    if (frame.Slider != null && nextFrame.Slider == frame.Slider)
                    {
                        // Using j += FrameDelay here (as it should be) makes the cursor be slightly ahead of the slider ball.
                        // Replay interpolation issue? Using FrameDelay * 2 or / 2 looks correct even though
                        // logically it doesn't change the result in this regard.
                        // TODO: figure this out.

                        // Idea: instead of using fixed FrameDelay consider calculating the optimal time offset
                        // which keeps the Auto's cursor trail smooth through this slider's curve.
                        // Currently really fast sliders make Auto have non-smooth cursor trail.
                        for (double j = frame.Time + FrameDelay; j < nextFrame.Time; j += FrameDelay)
                        {
                            Vector2 pos = frame.Slider.PositionAt((j - frame.Slider.StartTime) / frame.Slider.Duration);
                            AddFrameToReplay(new ReplayFrame(j, pos.X, pos.Y, ReplayButtonState.Left1));
                        }
                    }
                }
            }

            if (FirstPass.Count > 0)
            {
                var lastFrame = FirstPass[FirstPass.Count - 1];
                AddFrameToReplay(new ReplayFrame(lastFrame.Time, lastFrame.Position.X, lastFrame.Position.Y, ReplayButtonState.Left1));
                AddFrameToReplay(new ReplayFrame(lastFrame.Time, lastFrame.Position.X, lastFrame.Position.Y, ReplayButtonState.None));
            }

            //for (int i = 0; i < Beatmap.HitObjects.Count; i++)
            //{
            //    OsuHitObject h = Beatmap.HitObjects[i];

            //    if (DelayedMovements && i > 0)
            //    {
            //        OsuHitObject prev = Beatmap.HitObjects[i - 1];
            //        addDelayedMovements(h, prev);
            //    }

            //    addHitObjectReplay(h);
            //}

            return Replay;
        }


        private void addDelayedMovements(OsuHitObject h, OsuHitObject prev)
        {
            double endTime = (prev as IHasEndTime)?.EndTime ?? prev.StartTime;

            // Make the cursor stay at a hitObject as long as possible (mainly for autopilot).
            if (h.StartTime - h.HitWindowFor(OsuScoreResult.Miss) > endTime + h.HitWindowFor(OsuScoreResult.Hit50) + 50)
            {
                if (!(prev is Spinner) && h.StartTime - endTime < 1000) AddFrameToReplay(new ReplayFrame(endTime + h.HitWindowFor(OsuScoreResult.Hit50), prev.StackedEndPosition.X, prev.StackedEndPosition.Y, ReplayButtonState.None));
                if (!(h is Spinner)) AddFrameToReplay(new ReplayFrame(h.StartTime - h.HitWindowFor(OsuScoreResult.Miss), h.StackedPosition.X, h.StackedPosition.Y, ReplayButtonState.None));
            }
            else if (h.StartTime - h.HitWindowFor(OsuScoreResult.Hit50) > endTime + h.HitWindowFor(OsuScoreResult.Hit50) + 50)
            {
                if (!(prev is Spinner) && h.StartTime - endTime < 1000) AddFrameToReplay(new ReplayFrame(endTime + h.HitWindowFor(OsuScoreResult.Hit50), prev.StackedEndPosition.X, prev.StackedEndPosition.Y, ReplayButtonState.None));
                if (!(h is Spinner)) AddFrameToReplay(new ReplayFrame(h.StartTime - h.HitWindowFor(OsuScoreResult.Hit50), h.StackedPosition.X, h.StackedPosition.Y, ReplayButtonState.None));
            }
            else if (h.StartTime - h.HitWindowFor(OsuScoreResult.Hit100) > endTime + h.HitWindowFor(OsuScoreResult.Hit100) + 50)
            {
                if (!(prev is Spinner) && h.StartTime - endTime < 1000) AddFrameToReplay(new ReplayFrame(endTime + h.HitWindowFor(OsuScoreResult.Hit100), prev.StackedEndPosition.X, prev.StackedEndPosition.Y, ReplayButtonState.None));
                if (!(h is Spinner)) AddFrameToReplay(new ReplayFrame(h.StartTime - h.HitWindowFor(OsuScoreResult.Hit100), h.StackedPosition.X, h.StackedPosition.Y, ReplayButtonState.None));
            }
        }

        private void addHitObjectReplay(OsuHitObject h)
        {
            // Default values for circles/sliders
            Vector2 startPosition = h.StackedPosition;
            EasingTypes easing = preferredEasing;
            float spinnerDirection = -1;

            // The startPosition for the slider should not be its .Position, but the point on the circle whose tangent crosses the current cursor position
            // We also modify spinnerDirection so it spins in the direction it enters the spin circle, to make a smooth transition.
            // TODO: Shouldn't the spinner always spin in the same direction?
            if (h is Spinner)
            {
                calcSpinnerStartPosAndDirection(Frames[Frames.Count - 1].Position, out startPosition, out spinnerDirection);

                Vector2 spinCentreOffset = SPINNER_CENTRE - Frames[Frames.Count - 1].Position;

                if (spinCentreOffset.Length > SPIN_RADIUS)
                {
                    // If moving in from the outside, don't ease out (default eases out). This means auto will "start" spinning immediately after moving into position.
                    easing = EasingTypes.In;
                }
            }

            // Do some nice easing for cursor movements
            if (Frames.Count > 0)
            {
                moveToHitObject(h.StartTime, startPosition, h.Radius, easing);
            }

            // Add frames to click the hitobject
            addHitObjectClickFrames(h, startPosition, spinnerDirection);
        }

        private void addImportantFrames(OsuHitObject h)
        {
            if (h is Slider)
            {
                var slider = h as Slider;

                // Sliders need a click on their start circle and a hold on each tick.
                AddFirstPassFrame(new FirstPassFrame(slider.Position, slider.StartTime, FrameType.CLICK, slider));

                foreach (var tick in slider.Ticks)
                {
                    AddFirstPassFrame(new FirstPassFrame(tick.Position, tick.StartTime, FrameType.SLIDER_TICK, slider));
                }

                // Repeats and slider ends are not required currently, but add them anyway.
                // TODO: add method in Slider to return repeats? Or, even better, add them to Ticks?
                var length = slider.Curve.Distance;
                var repeatDuration = length / slider.Velocity;
                for (int repeat = 1; repeat < slider.RepeatCount; repeat++)
                {
                    var repeatTime = repeat * repeatDuration;
                    AddFirstPassFrame(new FirstPassFrame(slider.PositionAt(repeatTime / slider.Duration), repeatTime + slider.StartTime, FrameType.SLIDER_TICK, slider));
                }

                // Assume there's a slider tick 36 ms before end like in osu!stable.
                var time = Math.Max(slider.StartTime + slider.Duration / 2, slider.EndTime - 36);
                AddFirstPassFrame(new FirstPassFrame(slider.PositionAt((time - slider.StartTime) / slider.Duration), time, FrameType.SLIDER_TICK, slider));

                AddFirstPassFrame(new FirstPassFrame(slider.EndPosition, slider.EndTime, FrameType.SLIDER_END, slider));
            }
            else if (h is Spinner)
            {
                // Spinners need to be added to Spins.
                AddSpin(h as Spinner);
            }
            else
            {
                // Normal circles are simply clicked.
                AddFirstPassFrame(new FirstPassFrame(h.Position, h.StartTime, FrameType.CLICK));
            }
        }

        private void addSpinFrames(Spin spin)
        {
            int prevFrameIndex = FirstPass.FindLastIndex((frame) => frame.Time <= spin.StartTime);

            // TODO: make the "else" part independent on Frames?
            Vector2 prevPos = (prevFrameIndex != -1) ? FirstPass[prevFrameIndex].Position
                                                     : Frames[Frames.Count - 1].Position;

            Vector2 startPosition;
            float spinnerDirection;
            calcSpinnerStartPosAndDirection(prevPos, out startPosition, out spinnerDirection);

            // Add the frames.
            Vector2 difference = startPosition - SPINNER_CENTRE;

            float radius = difference.Length;
            float angle = radius == 0 ? 0 : (float)Math.Atan2(difference.Y, difference.X);

            double t;

            for (double j = spin.StartTime + FrameDelay; j < spin.EndTime; j += FrameDelay)
            {
                t = ApplyModsToTime(j - spin.StartTime) * spinnerDirection;

                Vector2 pos = SPINNER_CENTRE + CirclePosition(t / 20 + angle, SPIN_RADIUS);
                AddFirstPassFrame(new FirstPassFrame(pos, j, FrameType.SPIN));
            }

            t = ApplyModsToTime(spin.EndTime - spin.StartTime) * spinnerDirection;
            Vector2 endPos = SPINNER_CENTRE + CirclePosition(t / 20 + angle, SPIN_RADIUS);
            AddFirstPassFrame(new FirstPassFrame(endPos, spin.EndTime, FrameType.SPIN));

            // TODO: perhaps also add spin frames at every click frame during the spin?
            // This way clicks won't interfere with spinning.
        }

        /// <summary>
        /// Resolve conflicts like multiple different slider ticks on one frame.
        /// </summary>
        private void resolveConflicts()
        {
            for (int i = 0; i < FirstPass.Count; i++)
            {
                int next;

                switch (FirstPass[i].Type)
                {
                    case FrameType.CLICK:
                        // These are fine as they are.
                        break;

                    case FrameType.SLIDER_TICK:
                        // We will be dealing with frames [i; next).
                        for (next = i + 1; next < FirstPass.Count; next++)
                        {
                            if (FirstPass[next].Time != FirstPass[i].Time || FirstPass[next].Type != FirstPass[i].Type)
                                break;
                        }

                        // TODO: if we have a conflict, check if we can satisfy all (some) coordinates at the same time
                        // for example, by positioning the cursor in the middle between two slider ticks.
                        // For now, simply drop all but the first slider tick.
                        FirstPass.RemoveRange(i + 1, next - i - 1);
                        next -= (next - i - 1);

                        // Get rid of any remaining spins or slider ends.
                        int temp = next;
                        for (; next < FirstPass.Count; next++)
                        {
                            if (FirstPass[next].Time != FirstPass[i].Time)
                                break;
                        }

                        FirstPass.RemoveRange(temp, next - temp);

                        break;

                    case FrameType.SPIN:
                        // Get rid of any remaining slider ends.
                        for (next = i + 1; next < FirstPass.Count; next++)
                        {
                            if (FirstPass[next].Time != FirstPass[i].Time)
                                break;
                        }

                        FirstPass.RemoveRange(i + 1, next - i - 1);

                        break;

                    case FrameType.SLIDER_END:
                        // If this slider end wasn't preceeded by a click or slider tick of the same slider, drop it.
                        // This heavily increases nice-looking-ness of Auto on 2B maps.
                        // The slider tick 36 ms before end prevents this from going overboard.
                        if (i <= 0 || FirstPass[i - 1].Slider != FirstPass[i].Slider)
                        {
                            FirstPass.RemoveAt(i);
                            i--;
                            continue;
                        }

                        // Same logic as slider ticks.
                        // We will be dealing with frames [i; next).
                        for (next = i + 1; next < FirstPass.Count; next++)
                        {
                            if (FirstPass[next].Time != FirstPass[i].Time)
                                break;
                        }

                        // TODO: if we have a conflict, check if we can satisfy all (some) coordinates at the same time
                        // for example, by positioning the cursor in the middle between two slider ends.
                        // For now, simply drop all but the first slider end.
                        FirstPass.RemoveRange(i + 1, next - i - 1);

                        break;
                }
            }
        }

        #endregion

        #region Helper subroutines

        private static void calcSpinnerStartPosAndDirection(Vector2 prevPos, out Vector2 startPosition, out float spinnerDirection)
        {
            Vector2 spinCentreOffset = SPINNER_CENTRE - prevPos;
            float distFromCentre = spinCentreOffset.Length;
            float distToTangentPoint = (float)Math.Sqrt(distFromCentre * distFromCentre - SPIN_RADIUS * SPIN_RADIUS);

            if (distFromCentre > SPIN_RADIUS)
            {
                // Previous cursor position was outside spin circle, set startPosition to the tangent point.

                // Angle between centre offset and tangent point offset.
                float angle = (float)Math.Asin(SPIN_RADIUS / distFromCentre);

                if (angle > 0)
                {
                    spinnerDirection = -1;
                }
                else
                {
                    spinnerDirection = 1;
                }

                // Rotate by angle so it's parallel to tangent line
                spinCentreOffset.X = spinCentreOffset.X * (float)Math.Cos(angle) - spinCentreOffset.Y * (float)Math.Sin(angle);
                spinCentreOffset.Y = spinCentreOffset.X * (float)Math.Sin(angle) + spinCentreOffset.Y * (float)Math.Cos(angle);

                // Set length to distToTangentPoint
                spinCentreOffset.Normalize();
                spinCentreOffset *= distToTangentPoint;

                // Move along the tangent line, now startPosition is at the tangent point.
                startPosition = prevPos + spinCentreOffset;
            }
            else if (spinCentreOffset.Length > 0)
            {
                // Previous cursor position was inside spin circle, set startPosition to the nearest point on spin circle.
                startPosition = SPINNER_CENTRE - spinCentreOffset * (SPIN_RADIUS / spinCentreOffset.Length);
                spinnerDirection = 1;
            }
            else
            {
                // Degenerate case where cursor position is exactly at the centre of the spin circle.
                startPosition = SPINNER_CENTRE + new Vector2(0, -SPIN_RADIUS);
                spinnerDirection = 1;
            }
        }

        private void moveToHitObject(double targetTime, Vector2 targetPos, double hitObjectRadius, EasingTypes easing)
        {
            ReplayFrame lastFrame = Frames[Frames.Count - 1];

            // Wait until Auto could "see and react" to the next note.
            double waitTime = targetTime - Math.Max(0.0, DrawableOsuHitObject.TIME_PREEMPT - reactionTime);
            if (waitTime > lastFrame.Time)
            {
                lastFrame = new ReplayFrame(waitTime, lastFrame.MouseX, lastFrame.MouseY, lastFrame.ButtonState);
                AddFrameToReplay(lastFrame);
            }

            Vector2 lastPosition = lastFrame.Position;

            double timeDifference = ApplyModsToTime(targetTime - lastFrame.Time);

            // Only "snap" to hitcircles if they are far enough apart. As the time between hitcircles gets shorter the snapping threshold goes up.
            if (timeDifference > 0 && // Sanity checks
                ((lastPosition - targetPos).Length > hitObjectRadius * (1.5 + 100.0 / timeDifference) || // Either the distance is big enough
                timeDifference >= 266)) // ... or the beats are slow enough to tap anyway.
            {
                // Perform eased movement
                for (double time = lastFrame.Time + FrameDelay; time < targetTime; time += FrameDelay)
                {
                    Vector2 currentPosition = Interpolation.ValueAt(time, lastPosition, targetPos, lastFrame.Time, targetTime, easing);
                    AddFrameToReplay(new ReplayFrame((int)time, currentPosition.X, currentPosition.Y, lastFrame.ButtonState));
                }

                buttonIndex = 0;
            }
            else
            {
                buttonIndex++;
            }
        }

        // Add frames to click the hitobject
        private void addHitObjectClickFrames(OsuHitObject h, Vector2 startPosition, float spinnerDirection)
        {
            // Time to insert the first frame which clicks the object
            // Here we mainly need to determine which button to use
            ReplayButtonState button = buttonIndex % 2 == 0 ? ReplayButtonState.Left1 : ReplayButtonState.Right1;

            ReplayFrame startFrame = new ReplayFrame(h.StartTime, startPosition.X, startPosition.Y, button);

            // TODO: Why do we delay 1 ms if the object is a spinner? There already is KEY_UP_DELAY from hEndTime.
            double hEndTime = ((h as IHasEndTime)?.EndTime ?? h.StartTime) + KEY_UP_DELAY;
            int endDelay = h is Spinner ? 1 : 0;
            ReplayFrame endFrame = new ReplayFrame(hEndTime + endDelay, h.StackedEndPosition.X, h.StackedEndPosition.Y, ReplayButtonState.None);

            // Decrement because we want the previous frame, not the next one
            int index = FindInsertionIndex(startFrame) - 1;

            // If the previous frame has a button pressed, force alternation.
            // If there are frames ahead, modify those to use the new button press.
            // Do we have a previous frame? No need to check for < replay.Count since we decremented!
            if (index >= 0)
            {
                ReplayFrame previousFrame = Frames[index];
                var previousButton = previousFrame.ButtonState;

                // If a button is already held, then we simply alternate
                if (previousButton != ReplayButtonState.None)
                {
                    Debug.Assert(previousButton != (ReplayButtonState.Left1 | ReplayButtonState.Right1), "Previous button state was not Left1 nor Right1 despite only using those two states.");

                    // Force alternation if we have the same button. Otherwise we can just keep the naturally to us assigned button.
                    if (previousButton == button)
                    {
                        button = (ReplayButtonState.Left1 | ReplayButtonState.Right1) & ~button;
                        startFrame.ButtonState = button;
                    }

                    // We always follow the most recent slider / spinner, so remove any other frames that occur while it exists.
                    int endIndex = FindInsertionIndex(endFrame);

                    if (index < Frames.Count - 1)
                        Frames.RemoveRange(index + 1, Math.Max(0, endIndex - (index + 1)));

                    // After alternating we need to keep holding the other button in the future rather than the previous one.
                    for (int j = index + 1; j < Frames.Count; ++j)
                    {
                        // Don't affect frames which stop pressing a button!
                        if (j < Frames.Count - 1 || Frames[j].ButtonState == previousButton)
                            Frames[j].ButtonState = button;
                    }
                }
            }

            AddFrameToReplay(startFrame);

            // We add intermediate frames for spinning / following a slider here.
            if (h is Spinner)
            {
                Spinner s = h as Spinner;

                Vector2 difference = startPosition - SPINNER_CENTRE;

                float radius = difference.Length;
                float angle = radius == 0 ? 0 : (float)Math.Atan2(difference.Y, difference.X);

                double t;

                for (double j = h.StartTime + FrameDelay; j < s.EndTime; j += FrameDelay)
                {
                    t = ApplyModsToTime(j - h.StartTime) * spinnerDirection;

                    Vector2 pos = SPINNER_CENTRE + CirclePosition(t / 20 + angle, SPIN_RADIUS);
                    AddFrameToReplay(new ReplayFrame((int)j, pos.X, pos.Y, button));
                }

                t = ApplyModsToTime(s.EndTime - h.StartTime) * spinnerDirection;
                Vector2 endPosition = SPINNER_CENTRE + CirclePosition(t / 20 + angle, SPIN_RADIUS);

                AddFrameToReplay(new ReplayFrame(s.EndTime, endPosition.X, endPosition.Y, button));

                endFrame.MouseX = endPosition.X;
                endFrame.MouseY = endPosition.Y;
            }
            else if (h is Slider)
            {
                Slider s = h as Slider;

                for (double j = FrameDelay; j < s.Duration; j += FrameDelay)
                {
                    Vector2 pos = s.PositionAt(j / s.Duration);
                    AddFrameToReplay(new ReplayFrame(h.StartTime + j, pos.X, pos.Y, button));
                }

                AddFrameToReplay(new ReplayFrame(s.EndTime, s.EndPosition.X, s.EndPosition.Y, button));
            }

            // We only want to let go of our button if we are at the end of the current replay. Otherwise something is still going on after us so we need to keep the button pressed!
            if (Frames[Frames.Count - 1].Time <= endFrame.Time)
                AddFrameToReplay(endFrame);
        }

        #endregion

        #region Utilities
        // Sorted by importance.
        private enum FrameType
        {
            CLICK = 0,
            SLIDER_TICK,
            SPIN,
            SLIDER_END
        }

        private struct FirstPassFrame
        {
            public Vector2 Position;
            public double Time;
            public FrameType Type;

            // If this frame is for handling a slider, this is the slider.
            public Slider Slider;

            public FirstPassFrame(Vector2 position, double time, FrameType type, Slider slider = null)
            {
                Position = position;
                Time = time;
                Type = type;
                Slider = slider;
            }
        }

        private class FirstPassFrameComparer : IComparer<FirstPassFrame>
        {
            public int Compare(FirstPassFrame f1, FirstPassFrame f2)
            {
                return f1.Time.CompareTo(f2.Time);
            }
        }

        private static readonly IComparer<FirstPassFrame> first_pass_frame_comparer = new FirstPassFrameComparer();

        private int FindFirstPassInsertionIndex(FirstPassFrame frame)
        {
            int index = FirstPass.BinarySearch(frame, first_pass_frame_comparer);

            if (index < 0)
            {
                index = ~index;
            }
            else
            {
                // Go to the first index which is actually bigger.
                // Also order the frames by their type.
                while (index < FirstPass.Count
                       && frame.Time == FirstPass[index].Time
                       && frame.Type > FirstPass[index].Type)
                {
                    ++index;
                }
            }

            return index;
        }

        private void AddFirstPassFrame(FirstPassFrame frame) => FirstPass.Insert(FindFirstPassInsertionIndex(frame), frame);

        private struct Spin
        {
            public double StartTime;
            public double EndTime;

            public Spin(double startTime, double endTime)
            {
                StartTime = startTime;
                EndTime = endTime;
            }
        }

        private void AddSpin(Spinner spinner)
        {
            var spin = new Spin(spinner.StartTime, spinner.EndTime);

            // If this spinner overlaps some other spin, merge them.
            // First, figure out the applicable bounds.
            int insert_after = Spins.FindLastIndex((s) => s.EndTime < spin.StartTime);
            int insert_before = Spins.FindIndex((s) => s.StartTime > spin.EndTime);
            if (insert_before == -1)
                insert_before = Spins.Count;

            // Now replace all spins between these bounds with one big spin.
            // For maps without overlapping spinners there will be no spins between.
            if (insert_before == insert_after + 1)
            {
                Spins.Insert(insert_after + 1, spin);
            }
            else
            {
                // Figure out the common interval.
                double startTime = Math.Min(Spins[insert_after + 1].StartTime, spin.StartTime);
                double endTime = Math.Max(Spins[insert_before - 1].EndTime, spin.EndTime);

                Spins.RemoveRange(insert_after + 1, insert_before - insert_after - 1);
                Spins.Insert(insert_after + 1, new Spin(startTime, endTime));
            }
        }
        #endregion
    }
}

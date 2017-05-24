// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using OpenTK;
using osu.Framework.Graphics.Sprites;
using osu.Game.Database;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Play;
using OpenTK.Graphics;
using osu.Desktop.VisualTests.Beatmaps;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Desktop.VisualTests.Tests
{
    internal class TestCaseReplay2B : TestCase
    {
        protected Player Player;
        private RulesetDatabase rulesets;

        public override string Description => @"Testing Auto's handling of concurrent objects.";

        [BackgroundDependencyLoader]
        private void load(BeatmapDatabase db, RulesetDatabase rulesets)
        {
            this.rulesets = rulesets;
        }

        public override void Reset()
        {
            base.Reset();

            Beatmap b = new Beatmap
            {
                HitObjects = GenerateObjects(),
                BeatmapInfo = new BeatmapInfo
                {
                    Difficulty = new BeatmapDifficulty(),
                    Ruleset = rulesets.Query<RulesetInfo>().First(),
                    Metadata = new BeatmapMetadata
                    {
                        Artist = @"Unknown",
                        Title = @"Sample Beatmap",
                        Author = @"peppy",
                    }
                }
            };

            Add(new Box
            {
                RelativeSizeAxes = Framework.Graphics.Axes.Both,
                Colour = Color4.Black,
            });

            Add(Player = CreatePlayer(new TestWorkingBeatmap(b)));
        }

        protected virtual Player CreatePlayer(WorkingBeatmap beatmap)
        {
            beatmap.Mods.Value = new Mod[] { new OsuModAutoplay() };

            return new Player
            {
                Beatmap = beatmap
            };
        }

        private List<HitObject> GenerateObjects()
        {
            var objects = new List<HitObject>();

            int time = 1500;

            // Two circles at the same time.
            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(0, 0)
            });

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(0, 100)
            });

            time += 1500;

            // Slider + circles.
            objects.Add(new Slider
            {
                StartTime = time,
                ControlPoints = new List<Vector2>
                {
                    new Vector2(100, 0),
                    new Vector2(300, 0),
                },
                Distance = 200,
                Position = new Vector2(100, 0),
                Velocity = 0.2
            });

            time += 250;

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(150, 100)
            });

            time += 500;

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(250, 100)
            });

            time += 1750;

            // Slider + circles ontop of tick and end.
            objects.Add(new Slider
            {
                StartTime = time,
                ControlPoints = new List<Vector2>
                {
                    new Vector2(100, 200),
                    new Vector2(300, 200),
                },
                Distance = 200,
                Position = new Vector2(100, 200),
                Velocity = 0.2
            });

            time += 500;

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(200, 300)
            });

            time += 500;

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(300, 300)
            });

            time += 1500;

            // Slider + slider, non-overlapping ticks.
            objects.Add(new Slider
            {
                StartTime = time,
                ControlPoints = new List<Vector2>
                {
                    new Vector2(100, 0),
                    new Vector2(300, 0),
                },
                Distance = 200,
                Position = new Vector2(100, 0),
                Velocity = 0.2
            });

            time += 250;

            objects.Add(new Slider
            {
                StartTime = time,
                ControlPoints = new List<Vector2>
                {
                    new Vector2(150, 200),
                    new Vector2(350, 200),
                },
                Distance = 200,
                Position = new Vector2(150, 200),
                Velocity = 0.2
            });

            time += 2250;

            // Slider + slider, overlapping ticks.
            objects.Add(new Slider
            {
                StartTime = time,
                ControlPoints = new List<Vector2>
                {
                    new Vector2(100, 0),
                    new Vector2(300, 0),
                },
                Distance = 200,
                Position = new Vector2(100, 0),
                Velocity = 0.2
            });

            objects.Add(new Slider
            {
                StartTime = time,
                ControlPoints = new List<Vector2>
                {
                    new Vector2(100, 200),
                    new Vector2(300, 200),
                },
                Distance = 200,
                Position = new Vector2(100, 200),
                Velocity = 0.2
            });

            time += 2500;

            // Spinner + different stuff.
            objects.Add(new Spinner
            {
                StartTime = time,
                EndTime = time + 5000,
                Position = OsuPlayfield.BASE_SIZE / 2
            });

            time += 250;

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(100, 0)
            });

            time += 250;

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(200, 0)
            });

            time += 250;

            objects.Add(new HitCircle
            {
                StartTime = time,
                Position = new Vector2(300, 0)
            });

            time += 250;

            objects.Add(new Slider
            {
                StartTime = time,
                ControlPoints = new List<Vector2>
                {
                    new Vector2(400, 0),
                    new Vector2(400, 200),
                },
                Distance = 200,
                Position = new Vector2(400, 0),
                Velocity = 0.2
            });

            objects.Sort((x, y) =>
            {
                var endTime1 = (x as IHasEndTime)?.EndTime ?? x.StartTime;
                var endTime2 = (y as IHasEndTime)?.EndTime ?? y.StartTime;

                return endTime1.CompareTo(endTime2);
            });
            return objects;
        }
    }
}

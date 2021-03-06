﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using OpenTK;
using OpenTK.Graphics;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Colour;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Database;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Framework.Graphics.Textures;
using System.Linq;
using osu.Framework.Input;

namespace osu.Game.Overlays.Direct
{
    public class DirectListPanel : DirectPanel
    {
        private const float horizontal_padding = 10;
        private const float vertical_padding = 5;
        private const float height = 70;

        public DirectListPanel(BeatmapSetInfo beatmap) : base(beatmap)
        {
            RelativeSizeAxes = Axes.X;
            Height = height;
            CornerRadius = 5;
            Masking = true;
            EdgeEffect = new EdgeEffect
            {
                Type = EdgeEffectType.Shadow,
                Offset = new Vector2(0f, 1f),
                Radius = 3f,
                Colour = Color4.Black.Opacity(0.25f),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            FadeInFromZero(200, EasingTypes.Out);
        }

        [BackgroundDependencyLoader]
        private void load(LocalisationEngine localisation, TextureStore textures)
        {
            Children = new[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                GetBackground(textures),
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    ColourInfo = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.25f), Color4.Black.Opacity(0.75f)),
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = vertical_padding, Bottom = vertical_padding, Left = horizontal_padding, Right = vertical_padding },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Current = localisation.GetUnicodePreference(SetInfo.Metadata.TitleUnicode, SetInfo.Metadata.Title),
                                    TextSize = 18,
                                    Font = @"Exo2.0-BoldItalic",
                                },
                                new OsuSpriteText
                                {
                                    Current = localisation.GetUnicodePreference(SetInfo.Metadata.ArtistUnicode, SetInfo.Metadata.Artist),
                                    Font = @"Exo2.0-BoldItalic",
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.X,
                                    Height = 20,
                                    Margin = new MarginPadding { Top = vertical_padding, Bottom = vertical_padding },
                                    Children = GetDifficultyIcons(),
                                },
                            },
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Margin = new MarginPadding { Right = height - vertical_padding * 2 + vertical_padding },
                            Children = new Drawable[]
                            {
                                new Statistic(FontAwesome.fa_play_circle, SetInfo.Beatmaps.FirstOrDefault()?.OnlineInfo.PlayCount ?? 0)
                                {
                                    Margin = new MarginPadding { Right = 1 },
                                },
                                new Statistic(FontAwesome.fa_heart, SetInfo.Beatmaps.FirstOrDefault()?.OnlineInfo.FavouriteCount ?? 0),
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Children = new[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Text = "mapped by ",
                                            TextSize = 14,
                                        },
                                        new OsuSpriteText
                                        {
                                            Text = SetInfo.Metadata.Author,
                                            TextSize = 14,
                                            Font = @"Exo2.0-SemiBoldItalic",
                                        },
                                    },
                                },
                                new OsuSpriteText
                                {
                                    Text = $"from {SetInfo.Metadata.Source}",
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    TextSize = 14,
                                    Alpha = string.IsNullOrEmpty(SetInfo.Metadata.Source) ? 0f : 1f,
                                },
                            },
                        },
                        new DownloadButton
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Size = new Vector2(height - vertical_padding * 2),
                        },
                    },
                },
            };
        }

        private class DownloadButton : ClickableContainer
        {
            private readonly TextAwesome icon;

            public DownloadButton()
            {
                Children = new Drawable[]
                {
                    icon = new TextAwesome
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        UseFullGlyphHeight = false,
                        TextSize = 30,
                        Icon = FontAwesome.fa_osu_chevron_down_o,
                    },
                };
            }

            protected override bool OnMouseDown(InputState state, MouseDownEventArgs args)
            {
                icon.ScaleTo(0.9f, 1000, EasingTypes.Out);
                return base.OnMouseDown(state, args);
            }

            protected override bool OnMouseUp(InputState state, MouseUpEventArgs args)
            {
                icon.ScaleTo(1f, 500, EasingTypes.OutElastic);
                return base.OnMouseUp(state, args);
            }

            protected override bool OnHover(InputState state)
            {
                icon.ScaleTo(1.1f, 500, EasingTypes.OutElastic);
                return base.OnHover(state);
            }

            protected override void OnHoverLost(InputState state)
            {
                icon.ScaleTo(1f, 500, EasingTypes.OutElastic);
            }
        }
    }
}

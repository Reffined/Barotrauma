﻿using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class CharacterHealth
    {
        private static bool toggledThisFrame;

        public static Sprite DamageOverlay;

        public static string DamageOverlayFile;

        private static string[] strengthTexts;

        private Point screenResolution;

        private float uiScale, inventoryScale;

        private Alignment alignment = Alignment.Right;
        public Alignment Alignment
        {
            get { return alignment; }
            set
            {
                if (alignment == value) { return; }
                alignment = value;
                UpdateAlignment();
            }
        }

        public GUIButton SuicideButton { get; private set; }

        // healthbars
        private GUIProgressBar healthBar;
        private GUIProgressBar healthBarShadow;
        private float healthShadowSize;
        private float healthShadowDelay;
        private float healthBarPulsateTimer;
        private float healthBarPulsatePhase;

        private float bloodParticleTimer;

        private GUIFrame healthWindow;

        private GUITextBlock deadIndicator;

        //private GUIComponent lowSkillIndicator;

        private GUIButton cprButton;

        private GUIListBox afflictionTooltip;

        private static readonly Color oxygenLowGrainColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        private SpriteSheet limbIndicatorOverlay;
        private float limbIndicatorOverlayAnimState;

        private SpriteSheet medUIExtra;
        private float medUIExtraAnimState;

        private GUIComponent draggingMed;

        private int highlightedLimbIndex = -1;
        private int selectedLimbIndex = -1;
        private LimbHealth currentDisplayedLimb;

        private GUIProgressBar healthWindowHealthBar;
        private GUIProgressBar healthWindowHealthBarShadow;

        private GUITextBlock characterName;
        private GUIListBox afflictionIconContainer;
        private GUILayoutGroup treatmentLayout;
        private GUIListBox recommendedTreatmentContainer;

        private float distortTimer;

        // 0-1
        private float damageIntensity;
        private readonly float damageIntensityDropdownRate = 0.1f;

        public float DamageOverlayTimer { get; private set; }

        private float updateDisplayedAfflictionsTimer;
        private const float UpdateDisplayedAfflictionsInterval = 0.5f;
        private List<Affliction> currentDisplayedAfflictions = new List<Affliction>();

        public float DisplayedVitality, DisplayVitalityDelay;

        public bool MouseOnElement
        {
            get { return highlightedLimbIndex > -1; }
        }

        private static CharacterHealth openHealthWindow;
        public static CharacterHealth OpenHealthWindow
        {
            get
            {
                return openHealthWindow;
            }
            set
            {
                if (openHealthWindow == value) { return; }
                if (value != null)
                {
                    if (!value.UseHealthWindow || value.Character.DisableHealthWindow) { return; }
                }

                var prevOpenHealthWindow = openHealthWindow;

                if (prevOpenHealthWindow != null)
                {
                    prevOpenHealthWindow.selectedLimbIndex = -1;
                    prevOpenHealthWindow.highlightedLimbIndex = -1;
                }

                openHealthWindow = value;
                toggledThisFrame = true;
                if (Character.Controlled == null) { return; }

                if (value == null &&
                    Character.Controlled?.SelectedCharacter?.CharacterHealth != null &&
                    Character.Controlled.SelectedCharacter.CharacterHealth == prevOpenHealthWindow/* &&
                    !Character.Controlled.SelectedCharacter.CanInventoryBeAccessed*/)
                {
                    Character.Controlled.DeselectCharacter();
                }

                Character.Controlled.ResetInteract = true;
                if (openHealthWindow != null)
                {
                    if (value.Character.Info == null || value.Character == Character.Controlled || Character.Controlled.HasEquippedItem("healthscanner"))
                    {
                        openHealthWindow.characterName.Text = value.Character.Name;
                    }
                    else
                    {
                        openHealthWindow.characterName.Text = value.Character.Info.DisplayName;
                        value.Character.Info.CheckDisguiseStatus(false);
                    }

                    if (Character.Controlled.SelectedConstruction != null && Character.Controlled.SelectedConstruction.GetComponent<Ladder>() == null)
                    {
                        Character.Controlled.SelectedConstruction = null;
                    }
                }

                HintManager.OnShowHealthInterface();
            }
        }

        public GUIButton CPRButton
        {
            get { return cprButton; }
        }

        public GUIComponent InventorySlotContainer
        {
            get;
            private set;
        }

        public float HealthBarPulsateTimer
        {
            get { return healthBarPulsateTimer; }
            set { healthBarPulsateTimer = MathHelper.Clamp(value, 0.0f, 10.0f); }
        }

        private GUIFrame healthBarHolder;

        partial void InitProjSpecific(XElement element, Character character)
        {
            DisplayedVitality = MaxVitality;

            if (strengthTexts == null)
            {
                strengthTexts = new string[]
                {
                    TextManager.Get("AfflictionStrengthLow"),
                    TextManager.Get("AfflictionStrengthMedium"),
                    TextManager.Get("AfflictionStrengthHigh")
                };
            }

            character.OnAttacked += OnAttacked;

            healthWindow = new GUIFrame(new RectTransform(new Vector2(0.35f, 0.6f), GUI.Canvas, anchor: Anchor.Center, scaleBasis: ScaleBasis.Smallest), style: "GUIFrameListBox");

            var healthWindowVerticalLayout = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.95f), healthWindow.RectTransform, Anchor.Center))
            {
                Stretch = true
            };

            var nameContainer = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.2f), healthWindowVerticalLayout.RectTransform) { MinSize = new Point(0, 20) }, isHorizontal: true)
            {
                Stretch = true
            };

            new GUICustomComponent(new RectTransform(new Vector2(0.2f, 1.0f), nameContainer.RectTransform, Anchor.CenterLeft),
                onDraw: (spriteBatch, component) =>
                {
                    character.Info?.DrawPortrait(spriteBatch, new Vector2(component.Rect.X, component.Rect.Center.Y - component.Rect.Width / 2), Vector2.Zero, component.Rect.Width, false, openHealthWindow?.Character != Character.Controlled);
                });
            characterName = new GUITextBlock(new RectTransform(new Vector2(0.6f, 1.0f), nameContainer.RectTransform), "", textAlignment: Alignment.CenterLeft, font: GUI.SubHeadingFont)
            {
                AutoScaleHorizontal = true
            };
            new GUICustomComponent(new RectTransform(new Vector2(0.2f, 1.0f), nameContainer.RectTransform),
                onDraw: (spriteBatch, component) =>
                {
                    character.Info?.DrawJobIcon(spriteBatch, component.Rect, openHealthWindow?.Character != Character.Controlled);
                });


            var healthBarContainer = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.07f), healthWindowVerticalLayout.RectTransform), style: null);
            var healthBarIcon = new GUIFrame(new RectTransform(new Vector2(0.095f, 1.0f), healthBarContainer.RectTransform), style: "GUIHealthBarIcon");
            healthWindowHealthBarShadow = new GUIProgressBar(new RectTransform(new Vector2(0.91f, 1.0f), healthBarContainer.RectTransform, Anchor.CenterRight),
                barSize: 1.0f, color: GUI.Style.Green, style: "GUIHealthBar")
            {
                IsHorizontal = true
            };
            healthWindowHealthBar = new GUIProgressBar(new RectTransform(new Vector2(0.91f, 1.0f), healthBarContainer.RectTransform, Anchor.CenterRight),
                barSize: 1.0f, color: GUI.Style.Green, style: "GUIHealthBar")
            {
                IsHorizontal = true
            };

            //spacing
            new GUIFrame(new RectTransform(new Vector2(1.0f, 0.05f), healthWindowVerticalLayout.RectTransform), style: null);

            var characterIndicatorArea = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.95f), healthWindowVerticalLayout.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                //RelativeSpacing = 0.05f
            };

            InventorySlotContainer = new GUICustomComponent(new RectTransform(new Vector2(0.1f, 1.0f), characterIndicatorArea.RectTransform, Anchor.TopLeft, Pivot.TopRight),
                (spriteBatch, component) =>
                {
                    for (int i = 0; i < character.Inventory.Capacity; i++)
                    {
                        if (character.Inventory.SlotTypes[i] != InvSlotType.HealthInterface || Character.Controlled != Character) { continue; }

                        //don't draw the item if it's being dragged out of the slot
                        bool drawItem = !Inventory.DraggingItems.Any() || !Character.Inventory.GetItemsAt(i).All(it => Inventory.DraggingItems.Contains(it)) || character.Inventory.visualSlots[i].MouseOn();

                        Inventory.DrawSlot(spriteBatch, Character.Inventory, Character.Inventory.visualSlots[i], Character.Inventory.GetItemAt(i), i, drawItem, Character.Inventory.SlotTypes[i]);

                        if (medUIExtra != null) 
                        { 
                            float overlayScale = Math.Min(
                                Character.Inventory.visualSlots[i].Rect.Width / (float)medUIExtra.FrameSize.X,
                                Character.Inventory.visualSlots[i].Rect.Height / (float)medUIExtra.FrameSize.Y);

                            int frame = (int)medUIExtraAnimState;

                            medUIExtra.Draw(spriteBatch, frame, Character.Inventory.visualSlots[i].Rect.Center.ToVector2(), Color.Gray, origin: medUIExtra.FrameSize.ToVector2() / 2, rotate: 0.0f,
                                scale: Vector2.One * overlayScale);
                        }
                    }
                },
                (dt, component) =>
                {
                    if (!GameMain.Instance.Paused)
                    {
                        medUIExtraAnimState = (medUIExtraAnimState + dt * 10.0f) % 16.0f;
                    }
                });

            var limbSelection = new GUICustomComponent(new RectTransform(new Vector2(0.4f, 1.0f), characterIndicatorArea.RectTransform),
                (spriteBatch, component) =>
                {
                    DrawHealthWindow(spriteBatch, component.RectTransform.Rect, true);
                },
                (deltaTime, component) =>
                {
                    UpdateLimbIndicators(deltaTime, component.RectTransform.Rect);
                }
            );
            deadIndicator = new GUITextBlock(new RectTransform(new Vector2(0.9f, 0.1f), limbSelection.RectTransform, Anchor.Center),
                text: TextManager.Get("Deceased"), font: GUI.LargeFont, textAlignment: Alignment.Center, style: "GUIToolTip")
            {
                Visible = false,
                CanBeFocused = false
            };
            if (deadIndicator.Text.Contains(' '))
            {
                deadIndicator.Wrap = true;
            }
            else
            {
                deadIndicator.AutoScaleHorizontal = true;
            }

            afflictionIconContainer = new GUIListBox(new RectTransform(new Vector2(0.25f, 0.7f), characterIndicatorArea.RectTransform), style: null);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), healthWindowVerticalLayout.RectTransform),
                TextManager.Get("SuitableTreatments"), font: GUI.SubHeadingFont, textAlignment: Alignment.BottomCenter);

            treatmentLayout = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.15f), healthWindowVerticalLayout.RectTransform), true)
            {
                Stretch = false
            };

            recommendedTreatmentContainer = new GUIListBox(new RectTransform(new Vector2(1.0f, 1.0f), treatmentLayout.RectTransform, Anchor.Center, Pivot.Center), isHorizontal: true, style: null)
            {
                Spacing = GUI.IntScale(4),
                KeepSpaceForScrollBar = false,
                ScrollBarVisible = false,
                AutoHideScrollBar = false
            };
            new GUITextBlock(new RectTransform(Vector2.One, recommendedTreatmentContainer.Content.RectTransform), TextManager.Get("none"), textAlignment: Alignment.Center)
            {
                CanBeFocused = false
            };

            characterIndicatorArea.Recalculate();

            cprButton = new GUIButton(new RectTransform(new Vector2(afflictionIconContainer.RectTransform.RelativeSize.X, 0.3f), characterIndicatorArea.RectTransform, Anchor.BottomRight, scaleBasis: ScaleBasis.Smallest), text: "", style: "CPRButton")
            {
                OnClicked = (button, userData) =>
                {
                    Character selectedCharacter = Character.Controlled?.SelectedCharacter;
                    if (selectedCharacter == null || (!selectedCharacter.IsUnconscious && selectedCharacter.Stun <= 0.0f)) 
                    { 
                        return false; 
                    }

                    Character.Controlled.AnimController.Anim = (Character.Controlled.AnimController.Anim == AnimController.Animation.CPR) ?
                        AnimController.Animation.None : AnimController.Animation.CPR;

                    selectedCharacter.AnimController.ResetPullJoints();

                    if (GameMain.Client != null)
                    {
                        GameMain.Client.CreateEntityEvent(Character.Controlled, new object[] { NetEntityEvent.Type.Treatment });
                    }

                    return true;
                },
                ToolTip = TextManager.Get("doctor.cprobjective"),
                IgnoreLayoutGroups = true,
                Visible = false
            };

            healthBarHolder = new GUIFrame(new RectTransform(Point.Zero, GUI.Canvas), style: null)
            {
                HoverCursor = CursorState.Hand
            };

            healthBarHolder.RectTransform.AbsoluteOffset = HUDLayoutSettings.HealthBarArea.Location;
            healthBarHolder.RectTransform.NonScaledSize = HUDLayoutSettings.HealthBarArea.Size;
            healthBarHolder.RectTransform.RelativeOffset = Vector2.Zero;

            healthBarShadow = new GUIProgressBar(new RectTransform(Vector2.One, healthBarHolder.RectTransform, Anchor.BottomRight),
                barSize: 1.0f, color: Color.Green, style: "CharacterHealthBar", showFrame: false)
            {
                Visible = false
            };
            healthShadowSize = 1.0f;

            healthBar = new GUIProgressBar(new RectTransform(Vector2.One, healthBarHolder.RectTransform, Anchor.BottomRight),
                barSize: 1.0f, color: GUI.Style.HealthBarColorHigh, style: "CharacterHealthBar")
            {
                HoverCursor = CursorState.Hand,
                ToolTip = TextManager.GetWithVariable("hudbutton.healthinterface", "[key]", GameMain.Config.KeyBindText(InputType.Health)),
                Enabled = true
            };

            UpdateAlignment();

            SuicideButton = new GUIButton(new RectTransform(new Vector2(0.1f, 0.02f), GUI.Canvas, Anchor.TopCenter)
            {
                MinSize = new Point(150, 20), RelativeOffset = new Vector2(0.0f, 0.01f)
            },
                TextManager.Get("GiveInButton"), style: "GUIButtonLarge")
            {
                Visible = false,
                ToolTip = TextManager.Get(GameMain.NetworkMember == null ? "GiveInHelpSingleplayer" : "GiveInHelpMultiplayer"),
                OnClicked = (button, userData) =>
                {
                    GUI.ForceMouseOn(null);
                    if (Character.Controlled != null)
                    {
                        if (GameMain.Client != null)
                        {
                            GameMain.Client.CreateEntityEvent(Character.Controlled, new object[] { NetEntityEvent.Type.Status });
                        }
                        else
                        {
                            var (type, affliction) = GetCauseOfDeath();
                            Character.Controlled.Kill(type, affliction);
                            Character.Controlled = null;
                        }
                    }
                    return true;
                }
            };
            SuicideButton.TextBlock.AutoScaleHorizontal = true;

            if (element != null)
            {
                foreach (XElement subElement in element.Elements())
                {
                    switch (subElement.Name.ToString().ToLowerInvariant())
                    {
                        case "sprite":
                        case "meduisilhouette":
                            limbIndicatorOverlay = new SpriteSheet(subElement);
                            break;
                        case "meduiextra":
                            medUIExtra = new SpriteSheet(subElement);
                            break;
                    }
                }
            }
        }

        private void OnAttacked(Character attacker, AttackResult attackResult)
        {
            if (Math.Abs(attackResult.Damage) < 0.01f) { return; }
            DamageOverlayTimer = MathHelper.Clamp(attackResult.Damage / MaxVitality, DamageOverlayTimer, 1.0f);
            if (healthShadowDelay <= 0.0f) { healthShadowDelay = 1.0f; }

            if (healthBarPulsateTimer <= 0.0f) { healthBarPulsatePhase = 0.0f; }
            healthBarPulsateTimer = 1.0f;

            float additionalIntensity = MathHelper.Lerp(0, 1, MathUtils.InverseLerp(0, 0.1f, attackResult.Damage / MaxVitality));
            damageIntensity = MathHelper.Clamp(damageIntensity + additionalIntensity, 0, 1);

            DisplayVitalityDelay = 0.5f;
        }

        private void UpdateAlignment()
        {
            screenResolution = new Point(GameMain.GraphicsWidth, GameMain.GraphicsHeight);
            inventoryScale = Inventory.UIScale;
            uiScale = GUI.Scale;

            healthBarHolder.RectTransform.AbsoluteOffset = HUDLayoutSettings.HealthBarArea.Location;
            healthBarHolder.RectTransform.NonScaledSize = HUDLayoutSettings.HealthBarArea.Size;
            healthBarHolder.RectTransform.RelativeOffset = Vector2.Zero;

            switch (alignment)
            {
                case Alignment.Left:
                    healthWindow.RectTransform.SetPosition(Anchor.BottomLeft);
                    break;
                case Alignment.Right:
                    healthWindow.RectTransform.SetPosition(Anchor.BottomRight);
                    break;
            }

            healthWindow.RectTransform.AbsoluteOffset = new Point(HUDLayoutSettings.Padding, screenResolution.Y - HUDLayoutSettings.ChatBoxArea.Y + HUDLayoutSettings.Padding);
            healthWindow.RectTransform.RecalculateChildren(false);
        }

        public void UpdateClientSpecific(float deltaTime)
        {
            if (GameMain.NetworkMember == null)
            {
                DisplayedVitality = Vitality;
            }
            else
            {
                DisplayVitalityDelay -= deltaTime;
                if (DisplayVitalityDelay <= 0.0f)
                {
                    DisplayedVitality = Vitality;
                }
            }
            
            if (damageIntensity > 0)
            {
                damageIntensity -= deltaTime * damageIntensityDropdownRate;
                if (damageIntensity < 0)
                {
                    damageIntensity = 0;
                }
            }
            if (DamageOverlayTimer > 0.0f)
            {
                DamageOverlayTimer -= deltaTime;
            }
        }

        private float timeUntilNextHeartbeatSound = 0.0f;
        private bool nextHeartbeatSoundIsSystole = true;
        private const string diastoleSoundTag = "heartbeatdiastole", systoleSoundTag = "heartbeatsystole";

        partial void UpdateOxygenProjSpecific(float prevOxygen, float deltaTime)
        {
            if (prevOxygen > 0.0f && OxygenAmount <= 0.0f && Character.Controlled == Character)
            {
                SoundPlayer.PlaySound(Character.Info != null && Character.Info.Gender == Gender.Female ? "drownfemale" : "drownmale");
            }

            if (Character == Character.Controlled && !IsUnconscious && !Character.IsDead && OxygenAmount < LowOxygenThreshold)
            {
                timeUntilNextHeartbeatSound -= deltaTime;
                if (timeUntilNextHeartbeatSound < 0.0f)
                {
                    if (nextHeartbeatSoundIsSystole)
                    {
                        SoundPlayer.PlaySound(systoleSoundTag, 1.0f - (OxygenAmount / LowOxygenThreshold));
                        timeUntilNextHeartbeatSound = MathHelper.Lerp(0.18f, 0.3f, Math.Clamp(OxygenAmount / InsufficientOxygenThreshold, 0.0f, 1.0f));
                    }
                    else
                    {
                        SoundPlayer.PlaySound(diastoleSoundTag, 1.0f - (OxygenAmount / LowOxygenThreshold));
                        timeUntilNextHeartbeatSound = MathHelper.Lerp(0.3f, 0.5f, Math.Clamp(OxygenAmount / InsufficientOxygenThreshold, 0.0f, 1.0f));
                    }
                    nextHeartbeatSoundIsSystole = !nextHeartbeatSoundIsSystole;
                }
            }
        }

        partial void UpdateBleedingProjSpecific(AfflictionBleeding affliction, Limb targetLimb, float deltaTime)
        {
            if (Character.InvisibleTimer > 0.0f) { return; }

            bloodParticleTimer -= deltaTime * (affliction.Strength / 10.0f);
            if (bloodParticleTimer <= 0.0f)
            {
                bool inWater = Character.AnimController.InWater;
                var drawTarget = inWater ? Particles.ParticlePrefab.DrawTargetType.Water : Particles.ParticlePrefab.DrawTargetType.Air;
                var emitter = Character.BloodEmitters.FirstOrDefault(e => e.Prefab.ParticlePrefab.DrawTarget == drawTarget || e.Prefab.ParticlePrefab.DrawTarget == Particles.ParticlePrefab.DrawTargetType.Both);
                float particleMinScale = emitter?.Prefab.Properties.ScaleMin ?? 0.5f;
                float particleMaxScale = emitter?.Prefab.Properties.ScaleMax ?? 1;
                float severity = Math.Min(affliction.Strength / affliction.Prefab.MaxStrength * Character.Params.BleedParticleMultiplier, 1);
                float bloodParticleSize = MathHelper.Lerp(particleMinScale, particleMaxScale, severity);

                Vector2 velocity = Rand.Vector(affliction.Strength * 0.1f);
                if (!inWater)
                {
                    bloodParticleSize *= 2.0f;
                    velocity = targetLimb.LinearVelocity * 100.0f;
                }

                // TODO: use the blood emitter?
                var blood = GameMain.ParticleManager.CreateParticle(
                    inWater ? Character.Params.BleedParticleWater : Character.Params.BleedParticleAir,
                    targetLimb.WorldPosition, velocity, 0.0f, Character.AnimController.CurrentHull);

                if (blood != null && !inWater)
                {
                    blood.Size *= bloodParticleSize;
                    if (!string.IsNullOrEmpty(Character.BloodDecalName) && Rand.Range(0.0f, 1.0f) < 0.05f)
                    {
                        blood.OnCollision += (Vector2 pos, Hull hull) =>
                        {
                            var decal = hull?.AddDecal(Character.BloodDecalName, pos, Rand.Range(1.0f, 2.0f), isNetworkEvent: true);
                            if (decal != null)
                            {
                                decal.FadeTimer = decal.LifeTime - decal.FadeOutTime * 2;
                            }
                        };
                    }
                }
                bloodParticleTimer = MathHelper.Lerp(2, 0.5f, severity);
            }
        }

        public static bool IsMouseOnHealthBar()
        {
            if (Character.Controlled?.CharacterHealth == null) { return false; }
            return Character.Controlled.CharacterHealth.healthBar.State == GUIComponent.ComponentState.Hover;
        }

        public void UpdateHUD(float deltaTime)
        {
            if (GUI.DisableHUD) return;
            if (openHealthWindow != null)
            {
                if (openHealthWindow != Character.Controlled?.CharacterHealth && openHealthWindow != Character.Controlled?.SelectedCharacter?.CharacterHealth)
                {
                    openHealthWindow = null;
                    return;
                }
            }

            bool forceAfflictionContainerUpdate = false;
            if (updateDisplayedAfflictionsTimer > 0.0f)
            {
                updateDisplayedAfflictionsTimer -= deltaTime;
            }
            else
            {
                forceAfflictionContainerUpdate = true;
                currentDisplayedAfflictions = GetAllAfflictions(mergeSameAfflictions: true)
                    .FindAll(a => a.ShouldShowIcon(Character) && a.Prefab.Icon != null);
                currentDisplayedAfflictions.Sort((a1, a2) =>
                {
                    int dmgPerSecond = Math.Sign(a2.DamagePerSecond - a1.DamagePerSecond);
                    return dmgPerSecond != 0 ? dmgPerSecond : Math.Sign(a1.Strength - a1.Strength);
                });
                HintManager.OnAfflictionDisplayed(Character, currentDisplayedAfflictions);
                updateDisplayedAfflictionsTimer = UpdateDisplayedAfflictionsInterval;
            }
            
            if (healthShadowDelay > 0.0f)
            {
                healthShadowDelay -= deltaTime;
            }
            else
            {
                healthShadowSize = healthBar.BarSize > healthShadowSize ?
                    Math.Min(healthShadowSize + deltaTime, healthBar.BarSize) :
                    Math.Max(healthShadowSize - deltaTime, healthBar.BarSize);
            }
            
            float blurStrength = 0.0f;
            float distortStrength = 0.0f;
            float distortSpeed = 0.0f;
            float radialDistortStrength = 0.0f;
            float chromaticAberrationStrength = 0.0f;
            float grainStrength = 0.0f;
            Color grainColor = Color.Transparent;

            float oxygenLowStrength = 0.0f;
            if (Character.IsUnconscious)
            {
                blurStrength = 1.0f;
                distortSpeed = 1.0f;
            }
            else if (OxygenAmount < 100.0f)
            {
                oxygenLowStrength = Math.Min(1.0f - (OxygenAmount - LowOxygenThreshold) / LowOxygenThreshold, 1.0f);
                blurStrength = MathHelper.Lerp(0.5f, 1.0f, 1.0f - Vitality / MaxVitality) * oxygenLowStrength;
                distortStrength = blurStrength * oxygenLowStrength;
                distortSpeed = blurStrength + 1.0f;
                distortSpeed *= distortSpeed * distortSpeed * distortSpeed;

                grainStrength = MathHelper.Lerp(0.5f, 10.0f, oxygenLowStrength);
                grainColor = oxygenLowGrainColor;
            }

            foreach (Affliction affliction in afflictions)
            {
                distortStrength = Math.Max(distortStrength, affliction.GetScreenDistortStrength());
                blurStrength = Math.Max(blurStrength, affliction.GetScreenBlurStrength());
                radialDistortStrength = Math.Max(radialDistortStrength, affliction.GetRadialDistortStrength());
                chromaticAberrationStrength = Math.Max(chromaticAberrationStrength, affliction.GetChromaticAberrationStrength());
                float afflictionGrainStrength = affliction.GetScreenGrainStrength();
                if (afflictionGrainStrength > 0.0f)
                {
                    grainStrength = Math.Max(grainStrength, affliction.GetScreenGrainStrength());
                    Color afflictionGrainColor = affliction.GetActiveEffect()?.GrainColor ?? Color.White;
                    grainColor = Color.Lerp(grainColor, afflictionGrainColor, (float)Math.Pow(1.0f - oxygenLowStrength, 2));
                }
            }
            foreach (LimbHealth limbHealth in limbHealths)
            {
                foreach (Affliction affliction in limbHealth.Afflictions)
                {
                    distortStrength = Math.Max(distortStrength, affliction.GetScreenDistortStrength());
                    blurStrength = Math.Max(blurStrength, affliction.GetScreenBlurStrength());
                    radialDistortStrength = Math.Max(radialDistortStrength, affliction.GetRadialDistortStrength());
                    chromaticAberrationStrength = Math.Max(chromaticAberrationStrength, affliction.GetChromaticAberrationStrength());
                }
            }

            Character.RadialDistortStrength = radialDistortStrength;
            Character.ChromaticAberrationStrength = chromaticAberrationStrength;
            Character.GrainStrength = grainStrength;
            Character.GrainColor = grainColor;
            if (blurStrength > 0.0f)
            {
                distortTimer = (distortTimer + deltaTime * distortSpeed) % MathHelper.TwoPi;
                Character.BlurStrength = (float)(Math.Sin(distortTimer) + 1.5f) * 0.25f * blurStrength;
                Character.DistortStrength = (float)(Math.Sin(distortTimer) + 1.0f) * 0.1f * distortStrength;
            }
            else
            {
                Character.BlurStrength = 0.0f;
                Character.DistortStrength = 0.0f;
                distortTimer = 0.0f;
            }

            if (PlayerInput.KeyHit(InputType.Health) && GUI.KeyboardDispatcher.Subscriber == null &&
                Character.Controlled.AllowInput && !toggledThisFrame)
            {
                if (openHealthWindow != null)
                {
                    OpenHealthWindow = null;
                }
                else if (Character.Controlled == Character && 
                    (Character.Controlled.FocusedCharacter?.CharacterHealth == null || !Character.Controlled.FocusedCharacter.CharacterHealth.UseHealthWindow || Character.Controlled.FocusedCharacter.DisableHealthWindow))
                {
                    OpenHealthWindow = this;
                    forceAfflictionContainerUpdate = true;
                }
            }
            else if (openHealthWindow == this)
            {
                if (HUD.CloseHUD(healthWindow.Rect))
                {
                    //emulate a Health input to get the character to deselect the item server-side
                    if (GameMain.Client != null)
                    {
                        Character.Controlled.Keys[(int)InputType.Health].Hit = true;
                    }
                    OpenHealthWindow = null;
                }

                foreach (GUIComponent afflictionIcon in afflictionIconContainer.Content.Children)
                {
                    if (!(afflictionIcon.UserData is Affliction affliction)) { continue; }
                    var btn = afflictionIcon.GetChild<GUIButton>();
                    if (affliction.AppliedAsFailedTreatmentTime > Timing.TotalTime - 1.0 && btn.FlashTimer <= 0.0f)
                    {
                        btn.Flash(GUI.Style.Red);
                    }
                    else if (affliction.AppliedAsSuccessfulTreatmentTime > Timing.TotalTime - 1.0 && btn.FlashTimer <= 0.0f)
                    {
                        btn.Flash(GUI.Style.Green);
                    }
                }

                if (GUI.MouseOn != null && GUI.MouseOn.UserData is string str && str == "selectaffliction")
                {
                    Affliction affliction = GUI.MouseOn.Parent.UserData as Affliction;

                    if (afflictionTooltip == null || afflictionTooltip.UserData != affliction)
                    {
                        afflictionTooltip = new GUIListBox(new RectTransform(new Vector2(0.4f, 0.2f), GUI.Canvas, scaleBasis: ScaleBasis.Smallest))
                        {
                            UserData = affliction,
                            CanBeFocused = false
                        };

                        CreateAfflictionInfoElements(afflictionTooltip.Content, affliction);

                        int height = afflictionTooltip.Content.Children.Sum(c => c.Rect.Height) + 10;
                        afflictionTooltip.RectTransform.Resize(new Point(afflictionTooltip.Rect.Width, height), true);
                        if (Alignment == Alignment.Right)
                        {
                            afflictionTooltip.RectTransform.AbsoluteOffset = new Point(GUI.MouseOn.Rect.X, GUI.MouseOn.Rect.Y);
                            afflictionTooltip.RectTransform.Pivot = Pivot.TopRight;
                        }
                        else
                        {
                            afflictionTooltip.RectTransform.AbsoluteOffset = new Point(GUI.MouseOn.Rect.Right, GUI.MouseOn.Rect.Y);
                            afflictionTooltip.RectTransform.Anchor = Anchor.TopLeft;
                        }

                        afflictionTooltip.ScrollBarVisible = false;

                        var labelContainer = afflictionTooltip.Content.GetChildByUserData("label");

                        labelContainer.RectTransform.Resize(new Point(labelContainer.Rect.Width, (int)(GUI.LargeFont.Size * 1.5f)));
                    }
                }
                else
                {
                    afflictionTooltip = null;
                }
            }
            toggledThisFrame = false;

            if (OpenHealthWindow == this)
            {
                var highlightedLimb = highlightedLimbIndex < 0 ? null : limbHealths[highlightedLimbIndex];
                if (highlightedLimbIndex < 0 && selectedLimbIndex < 0)
                {
                    // If no limb is selected or highlighted, select the one with the most critical afflictions.
                    var affliction = SortAfflictionsBySeverity(GetAllAfflictions(a => a.Prefab.IndicatorLimb != LimbType.None)).FirstOrDefault();
                    if (affliction.DamagePerSecond > 0 || affliction.Strength > 0)
                    {
                        var limbHealth = GetMatchingLimbHealth(affliction);
                        if (limbHealth != null)
                        {
                            selectedLimbIndex = limbHealths.IndexOf(limbHealth);
                        }
                    }
                    else
                    {
                        // If no affliction is critical, select the limb which has most damage.
                        var limbHealth = limbHealths.OrderByDescending(l => l.TotalDamage).FirstOrDefault();
                        selectedLimbIndex = limbHealths.IndexOf(limbHealth);
                    }
                }
                LimbHealth selectedLimb = selectedLimbIndex < 0 ? highlightedLimb : limbHealths[selectedLimbIndex];
                if (selectedLimb != currentDisplayedLimb || forceAfflictionContainerUpdate)
                {
                    UpdateAfflictionContainer(selectedLimb);
                    currentDisplayedLimb = selectedLimb;
                }

                foreach (GUIComponent component in recommendedTreatmentContainer.Content.Children)
                {
                    var treatmentButton = component.GetChild<GUIButton>();
                    if (!(treatmentButton?.UserData is ItemPrefab itemPrefab)) { continue; }
                    treatmentButton.Enabled = Character.Controlled.Inventory.AllItems.Any(it => it.prefab == itemPrefab);
                }
            }

            if (Character.IsDead)
            {
                healthBar.Color = healthWindowHealthBar.Color = Color.Black;
                healthBar.BarSize = healthWindowHealthBar.BarSize = 1.0f;
            }
            else
            {
                healthBar.Color = healthWindowHealthBar.Color = ToolBox.GradientLerp(DisplayedVitality / MaxVitality, GUI.Style.HealthBarColorLow, GUI.Style.HealthBarColorMedium, GUI.Style.HealthBarColorHigh);
                healthBar.HoverColor = healthWindowHealthBar.HoverColor = healthBar.Color * 2.0f;
                healthBar.BarSize = healthWindowHealthBar.BarSize = 
                    (DisplayedVitality > 0.0f) ? 
                    (MaxVitality > 0.0f ? DisplayedVitality / MaxVitality : 0.0f) : 
                    (Math.Abs(MinVitality) > 0.0f ? 1.0f - DisplayedVitality / MinVitality : 0.0f);

                if (healthBarPulsateTimer > 0.0f)
                {
                    //0-1
                    float pulsateAmount = (float)(Math.Sin(healthBarPulsatePhase) + 1.0f) / 2.0f;

                    healthBar.RectTransform.LocalScale = healthBarShadow.RectTransform.LocalScale = new Vector2(1.0f, (1.0f + pulsateAmount * healthBarPulsateTimer * 0.5f));
                    healthBarPulsatePhase += deltaTime * 5.0f;
                    healthBarPulsateTimer -= deltaTime;
                }
                else
                {
                    healthBar.RectTransform.LocalScale = Vector2.One;
                }
            }

            if (OpenHealthWindow == this)
            {
                if (Character == Character.Controlled && !Character.AllowInput)
                {
                    openHealthWindow = null;
                }

                if (Inventory.DraggingItems.Any())
                {
                    if (highlightedLimbIndex > -1)
                    {
                        selectedLimbIndex = highlightedLimbIndex;
                    }
                }

                if (draggingMed != null)
                {
                    if (!PlayerInput.PrimaryMouseButtonHeld())
                    {
                        OnItemDropped(draggingMed.UserData as Item, ignoreMousePos: false);
                        draggingMed = null;
                    }
                }
            }
            else
            {
                if (openHealthWindow != null && Character != Character.Controlled && Character != Character.Controlled?.SelectedCharacter)
                {
                    openHealthWindow = null;
                }
                highlightedLimbIndex = -1;
            }

            healthBarHolder.CanBeFocused = healthBar.CanBeFocused = healthBarShadow.CanBeFocused = !Character.ShouldLockHud();
            if (Character.AllowInput && UseHealthWindow && !Character.DisableHealthWindow && healthBar.Enabled && healthBar.CanBeFocused &&
                (GUI.IsMouseOn(healthBar) || highlightedAfflictionIcon != null) && Inventory.SelectedSlot == null)
            {
                healthBar.State = GUIComponent.ComponentState.Hover;
                if (PlayerInput.PrimaryMouseButtonClicked())
                {
                    OpenHealthWindow = openHealthWindow == this ? null : this;
                }
            }
            else
            {
                healthBar.State = GUIComponent.ComponentState.None;
            }

            SuicideButton.Visible = Character == Character.Controlled && !Character.IsDead && Character.IsIncapacitated;

            if (GameMain.GameSession?.Campaign is { } campaign)
            {
                RectTransform endRoundButton = campaign?.EndRoundButton?.RectTransform;
                RectTransform readyCheckButton = campaign?.ReadyCheckButton?.RectTransform;
                if (endRoundButton != null)
                {
                    if (SuicideButton.Visible)
                    {
                        Point offset = new Point(0, SuicideButton.Rect.Height);
                        endRoundButton.ScreenSpaceOffset = offset;
                    }
                    else if (endRoundButton.ScreenSpaceOffset != Point.Zero)
                    {
                        endRoundButton.ScreenSpaceOffset = Point.Zero;
                    }
                    if (readyCheckButton != null)
                    {
                        readyCheckButton.ScreenSpaceOffset = endRoundButton.ScreenSpaceOffset;
                    }
                }
            }

            cprButton.Visible =
                Character == Character.Controlled?.SelectedCharacter
                && !Character.IsDead
                && Character.IsKnockedDown
                && openHealthWindow == this;
            cprButton.Selected =  
                Character.Controlled != null && 
                Character == Character.Controlled.SelectedCharacter && 
                Character.Controlled.AnimController.Anim == AnimController.Animation.CPR;

            deadIndicator.Visible = Character.IsDead;
        }

        public void AddToGUIUpdateList()
        {
            if (GUI.DisableHUD) { return; }
            if (OpenHealthWindow == this)
            {
                healthWindow.AddToGUIUpdateList();
                afflictionTooltip?.AddToGUIUpdateList();
            }
            else if (Character.Controlled == Character && !CharacterHUD.IsCampaignInterfaceOpen)
            {
                healthBarHolder.AddToGUIUpdateList();          
            }
            if (SuicideButton.Visible && Character == Character.Controlled)
            {
                SuicideButton.AddToGUIUpdateList();
            }
            if (cprButton != null && cprButton.Visible)
            {
                cprButton.AddToGUIUpdateList();
            }
        }

        public void DrawHUD(SpriteBatch spriteBatch)
        {
            if (GUI.DisableHUD) { return; }
            if (GameMain.GraphicsWidth != screenResolution.X ||
                GameMain.GraphicsHeight != screenResolution.Y ||
                Math.Abs(inventoryScale - Inventory.UIScale) > 0.01f ||
                Math.Abs(uiScale - GUI.Scale) > 0.01f)
            {
                UpdateAlignment();
            }

            float damageOverlayAlpha = DamageOverlayTimer;
            if (Vitality < MaxVitality * 0.1f)
            {
                damageOverlayAlpha = Math.Max(1.0f - (Vitality / maxVitality * 10.0f), damageOverlayAlpha);
            }
            else
            {
                float pulsateAmount = (float)(Math.Sin(healthBarPulsatePhase) + 1.0f) / 2.0f;
                damageOverlayAlpha = pulsateAmount * healthBarPulsateTimer * damageIntensity;
            }

            if (damageOverlayAlpha > 0.0f)
            {
                DamageOverlay?.Draw(spriteBatch, Vector2.Zero, Color.White * damageOverlayAlpha, Vector2.Zero, 0.0f,
                    new Vector2(GameMain.GraphicsWidth / DamageOverlay.size.X, GameMain.GraphicsHeight / DamageOverlay.size.Y));
            }

            if (Character.Inventory != null)
            {
                healthBar.RectTransform.ScreenSpaceOffset = healthBarShadow.RectTransform.ScreenSpaceOffset = Point.Zero;
            }
            
            // If manning a turret the portrait doesn't get rendered so we push the health bar to remove the empty gap
            healthBarHolder.RectTransform.ScreenSpaceOffset = Character.ShouldLockHud() ? new Point(0, HUDLayoutSettings.PortraitArea.Height) : Point.Zero;

            DrawStatusHUD(spriteBatch);
        }


        private (Affliction affliction, string text)? highlightedAfflictionIcon;
        public void DrawStatusHUD(SpriteBatch spriteBatch)
        {
            highlightedAfflictionIcon = null;
            //Rectangle interactArea = healthBar.Rect;
            if (Character.Controlled?.SelectedCharacter == null && openHealthWindow == null)
            {
                List<(Affliction affliction, string text)> statusIcons = new List<(Affliction affliction, string text)>();
                if (Character.CurrentHull == null || Character.CurrentHull.LethalPressure > 5.0f)
                    statusIcons.Add((pressureAffliction, TextManager.Get("PressureHUDWarning")));
                if (Character.CurrentHull != null && Character.OxygenAvailable < LowOxygenThreshold && oxygenLowAffliction.Strength < oxygenLowAffliction.Prefab.ShowIconThreshold)
                    statusIcons.Add((oxygenLowAffliction, TextManager.Get("OxygenHUDWarning")));
                
                foreach (Affliction affliction in currentDisplayedAfflictions)
                {
                    statusIcons.Add((affliction, affliction.Prefab.Name));
                }

                Vector2 highlightedIconPos = Vector2.Zero;
                Rectangle afflictionArea = HUDLayoutSettings.AfflictionAreaLeft;

                // Push the icons down since the portrait doesn't get rendered
                if (Character.ShouldLockHud())
                {
                    afflictionArea.Y += HUDLayoutSettings.PortraitArea.Height;
                }

                bool horizontal = afflictionArea.Width > afflictionArea.Height;
                int iconSize = horizontal ? afflictionArea.Height : afflictionArea.Width;

                Point pos = new Point(afflictionArea.Right - iconSize, afflictionArea.Top);

                foreach (var statusIcon in statusIcons)
                {
                    Affliction affliction = statusIcon.affliction;
                    AfflictionPrefab afflictionPrefab = affliction.Prefab;

                    Rectangle afflictionIconRect = new Rectangle(pos, new Point(iconSize));
                    if (afflictionIconRect.Contains(PlayerInput.MousePosition) && !Character.ShouldLockHud() && GUI.MouseOn == null)
                    {
                        highlightedAfflictionIcon = statusIcon;
                        highlightedIconPos = afflictionIconRect.Location.ToVector2();
                    }

                    if (affliction.DamagePerSecond > 1.0f)
                    {
                        Rectangle glowRect = afflictionIconRect;
                        glowRect.Inflate((int)(20 * GUI.Scale), (int)(20 * GUI.Scale));
                        var glow = GUI.Style.GetComponentStyle("OuterGlowCircular");
                        glow.Sprites[GUIComponent.ComponentState.None][0].Draw(
                            spriteBatch, glowRect,
                            GUI.Style.Red * (float)((Math.Sin(affliction.DamagePerSecondTimer * MathHelper.TwoPi - MathHelper.PiOver2) + 1.0f) * 0.5f));
                    }

                    float alphaMultiplier = highlightedAfflictionIcon == statusIcon ? 1f : 0.8f;

                    afflictionPrefab.Icon?.Draw(spriteBatch,
                        pos.ToVector2(),
                        /*highlightedIcon == statusIcon ? statusIcon.First.Prefab.IconColor : statusIcon.First.Prefab.IconColor * 0.8f,*/ // OLD IMPLEMENTATION
                        GetAfflictionIconColor(afflictionPrefab, affliction) * alphaMultiplier,
                        rotate: 0,
                        scale: iconSize / afflictionPrefab.Icon.size.X);

                    if (horizontal)
                        pos.X -= iconSize + (int)(5 * GUI.Scale);
                    else
                        pos.Y += iconSize + (int)(5 * GUI.Scale);
                }

                if (highlightedAfflictionIcon != null)
                {
                    string nameTooltip = highlightedAfflictionIcon.Value.text;
                    Vector2 offset = GUI.Font.MeasureString(nameTooltip);

                    GUI.DrawString(spriteBatch,
                        alignment == Alignment.Left ? highlightedIconPos + offset : highlightedIconPos - offset,
                        nameTooltip,
                        Color.White * 0.8f, Color.Black * 0.5f);
                }

                if (Vitality > 0.0f)
                {
                    float currHealth = healthBar.BarSize;
                    Color prevColor = healthBar.Color;
                    healthBarShadow.BarSize = healthShadowSize;
                    healthBarShadow.Color = Color.Lerp(GUI.Style.Red, Color.Black, 0.5f);
                    healthBarShadow.Visible = true;
                    healthBar.BarSize = currHealth;
                    healthBar.Color = prevColor;
                }
                else
                {
                    healthBarShadow.Visible = false;
                }
            }
            else
            {
                if (Vitality > 0.0f)
                {
                    float currHealth = healthWindowHealthBar.BarSize;
                    Color prevColor = healthWindowHealthBar.Color;
                    healthWindowHealthBarShadow.BarSize = healthShadowSize;
                    healthWindowHealthBarShadow.Color = GUI.Style.Red;
                    healthWindowHealthBarShadow.Visible = true;
                    healthWindowHealthBar.BarSize = currHealth;
                    healthWindowHealthBar.Color = prevColor;
                }
                else
                {
                    healthWindowHealthBarShadow.Visible = false;
                }
            }
        }

        public static Color GetAfflictionIconColor(AfflictionPrefab prefab, Affliction affliction)
        {
            // No specific colors, use generic
            if (prefab.IconColors == null)
            {
                if (prefab.IsBuff)
                {
                    return ToolBox.GradientLerp(affliction.Strength / prefab.MaxStrength, GUI.Style.BuffColorLow, GUI.Style.BuffColorMedium, GUI.Style.BuffColorHigh);
                }
                else
                {
                    return ToolBox.GradientLerp(affliction.Strength / prefab.MaxStrength, GUI.Style.DebuffColorLow, GUI.Style.DebuffColorMedium, GUI.Style.DebuffColorHigh);
                }
            }
            else
            {
                return ToolBox.GradientLerp(affliction.Strength / prefab.MaxStrength, prefab.IconColors);
            }
        }

        public static Color GetAfflictionIconColor(Affliction affliction) => GetAfflictionIconColor(affliction.Prefab, affliction);

        private void UpdateAfflictionContainer(LimbHealth selectedLimb)
        {
            if (selectedLimb == null)
            {
                afflictionIconContainer.Content.ClearChildren();
                return;
            }
            var currentAfflictions = GetMatchingAfflictions(selectedLimb, a => a.ShouldShowIcon(Character));
            var displayedAfflictions = afflictionIconContainer.Content.Children.Select(c => c.UserData as Affliction);
            if (currentAfflictions.Any(a => !displayedAfflictions.Contains(a)) || 
                displayedAfflictions.Any(a => !currentAfflictions.Contains(a)))
            {
                CreateAfflictionInfos(currentAfflictions);
            }

            UpdateAfflictionInfos(displayedAfflictions);
        }

        private void CreateAfflictionInfos(IEnumerable<Affliction> afflictions)
        {
            afflictionIconContainer.ClearChildren();
            recommendedTreatmentContainer.Content.ClearChildren();
            
            float characterSkillLevel = Character.Controlled == null ? 0.0f : Character.Controlled.GetSkillLevel("medical");

            //key = item identifier
            //float = suitability
            Dictionary<string, float> treatmentSuitability = new Dictionary<string, float>();
            GetSuitableTreatments(treatmentSuitability, normalize: true, limb: selectedLimbIndex == -1 ? null : Character.AnimController.Limbs.Find(l => l.HealthIndex == selectedLimbIndex));

            foreach (string treatment in treatmentSuitability.Keys.ToList())
            {
                //prefer suggestions for items the player has
                if (Character.Controlled.Inventory.FindItemByIdentifier(treatment) != null)
                {
                    treatmentSuitability[treatment] *= 10.0f;
                }
            }

            Affliction mostSevereAffliction = SortAfflictionsBySeverity(afflictions).FirstOrDefault();
            GUIButton buttonToSelect = null;

            foreach (Affliction affliction in afflictions)
            {
                var child = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.3f), afflictionIconContainer.Content.RectTransform, Anchor.TopCenter))
                {
                    Stretch = true,
                    UserData = affliction
                };

                var button = new GUIButton(new RectTransform(new Vector2(1.0f, 0.9f), child.RectTransform), style: null)
                {
                    Color = Color.Gray.Multiply(0.1f).Opaque(),
                    HoverColor = Color.Gray.Multiply(0.4f).Opaque(),
                    SelectedColor = Color.Gray.Multiply(0.25f).Opaque(),
                    PressedColor = Color.Gray.Multiply(0.2f).Opaque(),
                    UserData = "selectaffliction",
                    OnClicked = SelectAffliction
                };

                if (affliction == mostSevereAffliction)
                {
                    buttonToSelect = button;
                }

                var afflictionIcon = new GUIImage(new RectTransform(Vector2.One * 0.8f, button.RectTransform, Anchor.Center), affliction.Prefab.Icon, scaleToFit: true)
                {
                    Color = GetAfflictionIconColor(affliction),
                    CanBeFocused = false
                };
                afflictionIcon.PressedColor = afflictionIcon.Color;
                afflictionIcon.HoverColor = Color.Lerp(afflictionIcon.Color, Color.White, 0.6f);
                afflictionIcon.SelectedColor = Color.Lerp(afflictionIcon.Color, Color.White, 0.5f);

                float afflictionVitalityDecrease = affliction.GetVitalityDecrease(this);

                Color afflictionEffectColor = Color.White;
                if (afflictionVitalityDecrease > 0.0f)
                {
                    afflictionEffectColor = GUI.Style.Red;
                }
                else if (afflictionVitalityDecrease < 0.0f)
                {
                    afflictionEffectColor = GUI.Style.Green;
                }

                var nameText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), child.RectTransform), 
                    affliction.Prefab.Name, font: GUI.SmallFont, textAlignment: Alignment.Center, style: "GUIToolTip");
                nameText.Text = ToolBox.LimitString(nameText.Text, nameText.Font, nameText.Rect.Width);
                nameText.RectTransform.MinSize = new Point(0, (int)(nameText.TextSize.Y * 1.25f));

                new GUIProgressBar(new RectTransform(new Vector2(1.0f, 0.15f), child.RectTransform), 0.0f, afflictionEffectColor, style: "GUIAfflictionBar")
                {
                    UserData = "afflictionstrength"
                };

                child.Recalculate();
            }

            if (!treatmentSuitability.Any())
            {
                new GUITextBlock(new RectTransform(Vector2.One, recommendedTreatmentContainer.Content.RectTransform), TextManager.Get("none"), textAlignment: Alignment.Center)
                {
                    CanBeFocused = false
                };
                recommendedTreatmentContainer.ScrollBarVisible = false;
                recommendedTreatmentContainer.AutoHideScrollBar = false;
            }
            else
            {
                recommendedTreatmentContainer.ScrollBarVisible = true;
                recommendedTreatmentContainer.AutoHideScrollBar = true;
            }

            buttonToSelect?.OnClicked(buttonToSelect, "selectaffliction");

            afflictionIconContainer.RecalculateChildren();

            List<KeyValuePair<string, float>> treatmentSuitabilities = treatmentSuitability.OrderByDescending(t => t.Value).ToList();

            int count = 0;
            foreach (KeyValuePair<string, float> treatment in treatmentSuitabilities)
            {
                count++;
                if (count > 5) { break; }
                if (!(MapEntityPrefab.Find(name: null, identifier: treatment.Key, showErrorMessages: false) is ItemPrefab item)) { continue; }

                var itemSlot = new GUIFrame(new RectTransform(new Vector2(1.0f / 6.0f, 1.0f), recommendedTreatmentContainer.Content.RectTransform, Anchor.TopLeft),
                    style: null)
                {
                    UserData = item
                };

                var innerFrame = new GUIButton(new RectTransform(Vector2.One, itemSlot.RectTransform, Anchor.Center, Pivot.Center, scaleBasis: ScaleBasis.Smallest), style: "GUIButtonRound")
                {
                    UserData = item,
                    ToolTip = $"‖color:255,255,255,255‖{item.Name}‖color:end‖" + '\n' + item.Description,
                    OnClicked = (btn, userdata) =>
                    {
                        if (!(userdata is ItemPrefab itemPrefab)) { return false; }
                        var item = Character.Controlled.Inventory.AllItems.FirstOrDefault(it => it.prefab == itemPrefab);
                        if (item == null) { return false; }
                        Limb targetLimb = Character.AnimController.Limbs.FirstOrDefault(l => l.HealthIndex == selectedLimbIndex);
                        item.ApplyTreatment(Character.Controlled, Character, targetLimb);
                        return true;
                    }
                };
                Sprite itemSprite = item.InventoryIcon ?? item.sprite;
                Color itemColor = itemSprite == item.sprite ? item.SpriteColor : item.InventoryIconColor;
                var itemIcon = new GUIImage(new RectTransform(new Vector2(0.8f, 0.8f), innerFrame.RectTransform, Anchor.Center),
                    itemSprite, scaleToFit: true)
                {
                    CanBeFocused = false,
                    Color = itemColor * 0.9f,
                    HoverColor = itemColor,
                    SelectedColor = itemColor,
                    DisabledColor = itemColor * 0.7f
                };
            }

            recommendedTreatmentContainer.RecalculateChildren();

            afflictionIconContainer.Content.RectTransform.SortChildren((r1, r2) =>
            {
                var first = r1.GUIComponent.UserData as Affliction;
                var second = r2.GUIComponent.UserData as Affliction;
                int dmgPerSecond = Math.Sign(second.DamagePerSecond - first.DamagePerSecond);
                return dmgPerSecond != 0 ? dmgPerSecond : Math.Sign(second.Strength - first.Strength);
            });
        }

        private void CreateAfflictionInfoElements(GUIComponent parent, Affliction affliction)
        {
            var labelContainer = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.2f), parent.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                AbsoluteSpacing = 10,
                UserData = "label",
                CanBeFocused = false
            };

            var afflictionName = new GUITextBlock(new RectTransform(new Vector2(0.65f, 1.0f), labelContainer.RectTransform), affliction.Prefab.Name, textAlignment: Alignment.CenterLeft, font: GUI.LargeFont)
            {
                CanBeFocused = false,
                AutoScaleHorizontal = true
            };
            var afflictionStrength = new GUITextBlock(new RectTransform(new Vector2(0.35f, 0.6f), labelContainer.RectTransform), "", textAlignment: Alignment.TopRight, font: GUI.SubHeadingFont)
            {
                UserData = "strength",
                CanBeFocused = false
            };
            var vitality = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.4f), labelContainer.RectTransform, Anchor.BottomRight), "", textAlignment: Alignment.BottomRight)
            {
                Padding = afflictionStrength.Padding,
                IgnoreLayoutGroups = true,
                UserData = "vitality",
                CanBeFocused = false
            };

            var description = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), parent.RectTransform),
                affliction.Prefab.Description, textAlignment: Alignment.TopLeft, wrap: true)
            {
                CanBeFocused = false
            };

            if (description.Font.MeasureString(description.WrappedText).Y > description.Rect.Height)
            {
                description.Font = GUI.SmallFont;
            }

            Point nameDims = new Point(afflictionName.Rect.Width, (int)(GUI.LargeFont.Size * 1.5f));

            afflictionStrength.Text = strengthTexts[
                MathHelper.Clamp((int)Math.Floor((affliction.Strength / affliction.Prefab.MaxStrength) * strengthTexts.Length), 0, strengthTexts.Length - 1)];

            Vector2 strengthDims = GUI.SubHeadingFont.MeasureString(afflictionStrength.Text);

            labelContainer.RectTransform.Resize(new Point(labelContainer.Rect.Width, nameDims.Y));
            afflictionName.RectTransform.Resize(new Point((int)(labelContainer.Rect.Width - strengthDims.X * 0.99f), nameDims.Y));
            afflictionStrength.RectTransform.Resize(new Point(labelContainer.Rect.Width - afflictionName.Rect.Width, nameDims.Y));
            
            afflictionStrength.TextColor = Color.Lerp(GUI.Style.Orange, GUI.Style.Red,
                affliction.Strength / affliction.Prefab.MaxStrength);

            description.RectTransform.Resize(new Point(description.Rect.Width, (int)(description.TextSize.Y + 10)));

            int vitalityDecrease = (int)affliction.GetVitalityDecrease(this);
            if (vitalityDecrease == 0)
            {
                vitality.Visible = false;
            }
            else
            {
                vitality.Visible = true;
                vitality.Text = TextManager.Get("Vitality") + " -" + vitalityDecrease;
                vitality.TextColor = vitalityDecrease <= 0 ? GUI.Style.Green :
                Color.Lerp(GUI.Style.Orange, GUI.Style.Red, affliction.Strength / affliction.Prefab.MaxStrength);
            }

            vitality.AutoDraw = true;
        }

        private bool SelectAffliction(GUIButton button, object userData)
        {
            bool selected = button.Selected;
            foreach (var child in afflictionIconContainer.Content.Children)
            {
                GUIButton btn = child.GetChild<GUIButton>();
                if (btn != null)
                {
                    btn.Selected = btn == button && !selected;
                }
            }

            return false;
        }

        private void UpdateAfflictionInfos(IEnumerable<Affliction> afflictions)
        {
            foreach (Affliction affliction in afflictions)
            {
                var child = afflictionIconContainer.Content.FindChild(affliction);
                var afflictionStrengthBar = child.GetChildByUserData("afflictionstrength") as GUIProgressBar;
                afflictionStrengthBar.BarSize = affliction.Strength / affliction.Prefab.MaxStrength;
                if (afflictionTooltip != null && afflictionTooltip.UserData == affliction)
                {
                    UpdateAfflictionInfo(afflictionTooltip.Content, affliction);
                }
            }
        }

        private void UpdateAfflictionInfo(GUIComponent parent, Affliction affliction)
        {
            var labelContainer = parent.GetChildByUserData("label");

            var strengthText = labelContainer.GetChildByUserData("strength") as GUITextBlock;

            strengthText.Text = strengthTexts[
                MathHelper.Clamp((int)Math.Floor((affliction.Strength / affliction.Prefab.MaxStrength) * strengthTexts.Length), 0, strengthTexts.Length - 1)];

            strengthText.TextColor = Color.Lerp(GUI.Style.Orange, GUI.Style.Red,
                affliction.Strength / affliction.Prefab.MaxStrength);

            var vitalityText = labelContainer.GetChildByUserData("vitality") as GUITextBlock;
            int vitalityDecrease = (int)affliction.GetVitalityDecrease(this);
            if (vitalityDecrease == 0)
            {
                vitalityText.Visible = false;
            }
            else
            {
                vitalityText.Visible = true;
                vitalityText.Text = TextManager.Get("Vitality") + " -" + vitalityDecrease;
                vitalityText.TextColor = vitalityDecrease <= 0 ? GUI.Style.Green :
                Color.Lerp(GUI.Style.Orange, GUI.Style.Red, affliction.Strength / affliction.Prefab.MaxStrength);
            }
        }

        public bool OnItemDropped(Item item, bool ignoreMousePos)
        {
            //items can be dropped outside the health window
            if (!ignoreMousePos &&
                !healthWindow.Rect.Contains(PlayerInput.MousePosition) )
            {
                return false;
            }

            //can't apply treatment to dead characters
            if (Character.IsDead) { return true; }
            if (item == null || !item.UseInHealthInterface) { return true; }
            if (!ignoreMousePos)
            {
                if (highlightedLimbIndex > -1)
                {
                    selectedLimbIndex = highlightedLimbIndex;
                }
            }

            Limb targetLimb = Character.AnimController.Limbs.FirstOrDefault(l => l.HealthIndex == selectedLimbIndex);

            item.ApplyTreatment(Character.Controlled, Character, targetLimb);

            return true;
        }
        private void UpdateLimbIndicators(float deltaTime, Rectangle drawArea)
        {
            if (!GameMain.Instance.Paused)
            {
                limbIndicatorOverlayAnimState += deltaTime * 8.0f;
            }

            highlightedLimbIndex = -1;
            int i = 0;
            foreach (LimbHealth limbHealth in limbHealths)
            {
                if (limbHealth.IndicatorSprite == null) continue;
                
                float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);

                Rectangle highlightArea = GetLimbHighlightArea(limbHealth, drawArea);

                if (highlightArea.Contains(PlayerInput.MousePosition))
                {
                    highlightedLimbIndex = i;
                }
                i++;
            }

            if (PlayerInput.PrimaryMouseButtonClicked() && highlightedLimbIndex > -1)
            {
                selectedLimbIndex = highlightedLimbIndex;
            }
        }

        private void DrawHealthWindow(SpriteBatch spriteBatch, Rectangle drawArea, bool allowHighlight)
        {
            if (Character.Removed) { return; }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, blendState: BlendState.NonPremultiplied, rasterizerState: GameMain.ScissorTestEnable, effect: GameMain.GameScreen.GradientEffect);

            int i = 0;
            foreach (LimbHealth limbHealth in limbHealths)
            {
                if (limbHealth.IndicatorSprite == null) continue;

                Rectangle limbEffectiveArea = new Rectangle(limbHealth.IndicatorSprite.SourceRect.X + limbHealth.HighlightArea.X,
                                                            limbHealth.IndicatorSprite.SourceRect.Y + limbHealth.HighlightArea.Y,
                                                            limbHealth.HighlightArea.Width,
                                                            limbHealth.HighlightArea.Height);

                float damageLerp = limbHealth.TotalDamage > 0.0f ? MathHelper.Lerp(0.2f, 1.0f, limbHealth.TotalDamage / 100.0f) : 0.0f;

                var tempAfflictions = GetMatchingAfflictions(limbHealth, a => true);

                float negativeEffect = tempAfflictions.Where(a => !a.Prefab.IsBuff && a.ShouldShowIcon(Character)).Sum(a => a.Strength);
                //float negativeMaxEffect = tempAfflictions.Where(a => !a.Prefab.IsBuff).Sum(a => a.Prefab.MaxStrength);
                float positiveEffect = tempAfflictions.Where(a => a.Prefab.IsBuff && a.ShouldShowIcon(Character)).Sum(a => a.Strength * 0.2f);
                //float positiveMaxEffect = tempAfflictions.Where(a => a.Prefab.IsBuff).Sum(a => a.Prefab.MaxStrength);

                float midPoint = (float)limbEffectiveArea.Center.Y / (float)limbHealth.IndicatorSprite.Texture.Height;
                float fadeDist = 0.6f * (float)limbEffectiveArea.Height / (float)limbHealth.IndicatorSprite.Texture.Height;

                if (negativeEffect > 0.0f && negativeEffect < 5.0f) { negativeEffect = 10.0f; }
                if (positiveEffect > 0.0f && positiveEffect < 5.0f) { positiveEffect = 10.0f; }

                Color positiveColor = Color.Lerp(Color.Orange, Color.Lime, Math.Min(positiveEffect / 25.0f, 1.0f));
                Color negativeColor = Color.Lerp(Color.Orange, Color.Red, Math.Min(negativeEffect / 25.0f, 1.0f));

                Color color1 = Color.Orange;
                Color color2 = Color.Orange;

                if (negativeEffect + positiveEffect > 0.0f)
                {
                    if (negativeEffect >= positiveEffect)
                    {
                        color1 = Color.Lerp(positiveColor, negativeColor, (negativeEffect - positiveEffect) / negativeEffect);
                        color2 = negativeColor;
                    }
                    else
                    {
                        color1 = positiveColor;
                        color2 = Color.Lerp(negativeColor, positiveColor, (positiveEffect - negativeEffect) / positiveEffect);
                    }
                }

                if (Character.IsDead)
                {
                    color1 = Color.Lerp(color1, Color.Black, 0.75f);
                    color2 = Color.Lerp(color2, Color.Black, 0.75f);
                }

                GameMain.GameScreen.GradientEffect.Parameters["color1"].SetValue(color1.ToVector4());
                GameMain.GameScreen.GradientEffect.Parameters["color2"].SetValue(color2.ToVector4());
                GameMain.GameScreen.GradientEffect.Parameters["midPoint"].SetValue(midPoint);
                GameMain.GameScreen.GradientEffect.Parameters["fadeDist"].SetValue(fadeDist);

                float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);

                limbHealth.IndicatorSprite.Draw(spriteBatch,
                    drawArea.Center.ToVector2(), Color.White,
                    limbHealth.IndicatorSprite.Origin,
                    0, scale);

                if (GameMain.DebugDraw)
                {
                    Rectangle highlightArea = GetLimbHighlightArea(limbHealth, drawArea);

                    GUI.DrawRectangle(spriteBatch, highlightArea, Color.Red, false);
                    GUI.DrawRectangle(spriteBatch, drawArea, Color.Red, false);
                }

                i++;
            }

            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, Lights.CustomBlendStates.Multiplicative);

            if (limbIndicatorOverlay != null)
            {
                float overlayScale = Math.Min(
                    drawArea.Width / (float)limbIndicatorOverlay.FrameSize.X,
                    drawArea.Height / (float)limbIndicatorOverlay.FrameSize.Y);

                int frame = 0;
                int frameCount = 17;
                if (limbIndicatorOverlayAnimState >= frameCount * 2) limbIndicatorOverlayAnimState = 0.0f;
                if (limbIndicatorOverlayAnimState < frameCount)
                {
                    frame = (int)limbIndicatorOverlayAnimState;
                }
                else
                {
                    frame = frameCount - (int)(limbIndicatorOverlayAnimState - (frameCount - 1));
                }

                limbIndicatorOverlay.Draw(spriteBatch, frame, drawArea.Center.ToVector2(), Color.Gray, origin: limbIndicatorOverlay.FrameSize.ToVector2() / 2, rotate: 0.0f,
                    scale: Vector2.One * overlayScale);
            }

            if (allowHighlight)
            {
                i = 0;
                foreach (LimbHealth limbHealth in limbHealths)
                {
                    if (limbHealth.HighlightSprite == null) { continue; }

                    float scale = Math.Min(drawArea.Width / (float)limbHealth.HighlightSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.HighlightSprite.SourceRect.Height);

                    int drawCount = 0;
                    if (i == highlightedLimbIndex) { drawCount++; }
                    if (i == selectedLimbIndex) { drawCount++; }
                    for (int j = 0; j < drawCount; j++)
                    {
                        limbHealth.HighlightSprite.Draw(spriteBatch,
                            drawArea.Center.ToVector2(), Color.White,
                            limbHealth.HighlightSprite.Origin,
                            0, scale);
                    }
                    i++;
                }
            }
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, blendState: BlendState.NonPremultiplied, rasterizerState: GameMain.ScissorTestEnable);

            i = 0;
            foreach (LimbHealth limbHealth in limbHealths)
            {
                IEnumerable<Affliction> thisAfflictions = limbHealth.Afflictions.Where(a => a.ShouldShowIcon(Character));
                thisAfflictions = thisAfflictions.Concat(afflictions.Where(a =>
                {
                    Limb indicatorLimb = Character.AnimController.GetLimb(a.Prefab.IndicatorLimb);
                    return indicatorLimb != null && indicatorLimb.HealthIndex == i && a.ShouldShowIcon(Character);
                }));

                if (thisAfflictions.Count() <= 0) { i++; continue; }
                if (limbHealth.IndicatorSprite == null) { continue; }

                float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);
                
                Rectangle highlightArea = GetLimbHighlightArea(limbHealth, drawArea);
                
                float iconScale = 0.25f * scale;
                Vector2 iconPos = highlightArea.Center.ToVector2();

                //Affliction mostSevereAffliction = thisAfflictions.FirstOrDefault(a => !a.Prefab.IsBuff && !thisAfflictions.Any(a2 => !a2.Prefab.IsBuff && a2.Strength > a.Strength)) ?? thisAfflictions.FirstOrDefault();
                Affliction mostSevereAffliction = SortAfflictionsBySeverity(thisAfflictions, excludeBuffs: false).FirstOrDefault();
                if (mostSevereAffliction != null) { DrawLimbAfflictionIcon(spriteBatch, mostSevereAffliction, iconScale, ref iconPos); }

                if (thisAfflictions.Count() > 1)
                {
                    string additionalAfflictionCount = $"+{thisAfflictions.Count() - 1}";
                    Vector2 displace = GUI.SubHeadingFont.MeasureString(additionalAfflictionCount);
                    GUI.SubHeadingFont.DrawString(spriteBatch, additionalAfflictionCount, iconPos + new Vector2(displace.X * 1.1f, -displace.Y * 0.45f), Color.Black * 0.75f);
                    GUI.SubHeadingFont.DrawString(spriteBatch, additionalAfflictionCount, iconPos + new Vector2(displace.X, -displace.Y * 0.5f), Color.White);
                }

                i++;
            }

            if (selectedLimbIndex > -1 && afflictionIconContainer.Content.CountChildren > 0)
            {
                LimbHealth limbHealth = limbHealths[selectedLimbIndex];
                if (limbHealth?.IndicatorSprite != null)
                {
                    Rectangle selectedLimbArea = GetLimbHighlightArea(limbHealth, drawArea);
                    GUI.DrawLine(spriteBatch,
                        new Vector2(afflictionIconContainer.Rect.X, afflictionIconContainer.Rect.Y),
                        selectedLimbArea.Center.ToVector2(),
                        Color.LightGray * 0.5f, width: 4);
                }
            }

            if (draggingMed != null)
            {
                GUIImage itemImage = draggingMed.GetChild<GUIImage>();
                float scale = Math.Min(40.0f / itemImage.Sprite.size.X, 40.0f / itemImage.Sprite.size.Y);
                itemImage.Sprite.Draw(spriteBatch, PlayerInput.MousePosition, itemImage.Color, 0, scale);
            }
        }

        private void DrawLimbAfflictionIcon(SpriteBatch spriteBatch, Affliction affliction, float iconScale, ref Vector2 iconPos)
        {
            if (!affliction.ShouldShowIcon(Character) || affliction.Prefab.Icon == null) { return; }
            Vector2 iconSize = affliction.Prefab.Icon.size * iconScale;

            float showIconThreshold = Character.Controlled?.CharacterHealth == this ? affliction.Prefab.ShowIconThreshold : affliction.Prefab.ShowIconToOthersThreshold;

            //afflictions that have a strength of less than 10 are faded out slightly
            float alpha = MathHelper.Lerp(0.3f, 1.0f,
                (affliction.Strength - showIconThreshold) / Math.Min(affliction.Prefab.MaxStrength - showIconThreshold, 10.0f));

            affliction.Prefab.Icon.Draw(spriteBatch, iconPos - iconSize / 2.0f, GetAfflictionIconColor(affliction) * alpha, 0, iconScale);
            iconPos += new Vector2(10.0f, 20.0f) * iconScale;
        }

        private Rectangle GetLimbHighlightArea(LimbHealth limbHealth, Rectangle drawArea)
        {
            float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);
            return new Rectangle(
                (int)(drawArea.Center.X - (limbHealth.IndicatorSprite.SourceRect.Width / 2 - limbHealth.HighlightArea.X) * scale),
                (int)(drawArea.Center.Y - (limbHealth.IndicatorSprite.SourceRect.Height / 2 - limbHealth.HighlightArea.Y) * scale),
                (int)(limbHealth.HighlightArea.Width * scale),
                (int)(limbHealth.HighlightArea.Height * scale));
        }

        public void SetHealthBarVisibility(bool value)
        {
            healthBarHolder.Visible = value;
        }

        private readonly List<(AfflictionPrefab afflictionPrefab, float strength)> newAfflictions = new List<(AfflictionPrefab afflictionPrefab, float strength)>();
        private readonly List<(LimbHealth limb, AfflictionPrefab afflictionPrefab, float strength)> newLimbAfflictions = new List<(LimbHealth limb, AfflictionPrefab afflictionPrefab, float strength)>();
        private readonly List<(AfflictionPrefab.PeriodicEffect effect, float timer)> newPeriodicEffects = new List<(AfflictionPrefab.PeriodicEffect effect, float timer)>();

        public void ClientRead(IReadMessage inc)
        {
            newAfflictions.Clear();
            byte afflictionCount = inc.ReadByte();
            for (int i = 0; i < afflictionCount; i++)
            {
                uint afflictionID = inc.ReadUInt32();
                AfflictionPrefab afflictionPrefab = AfflictionPrefab.Prefabs.Find(p => p.UIntIdentifier == afflictionID);
                if (afflictionPrefab == null)
                {
                    DebugConsole.ThrowError("Error while reading character health data: affliction with the uint ID " + afflictionID + " not found.");
                    //read the 8 bytes for affliction strength anyway to prevent messing up reading rest of the message
                    _ = inc.ReadRangedSingle(0.0f, 100.0f, 8);
                    int _periodicAfflictionCount = inc.ReadByte();
                    for (int j = 0; j < _periodicAfflictionCount; j++)
                    {
                        _ = inc.ReadByte();
                    }
                    continue;
                }
                float afflictionStrength = inc.ReadRangedSingle(0.0f, afflictionPrefab.MaxStrength, 8);
                int periodicAfflictionCount = inc.ReadByte();
                for (int j = 0; j < periodicAfflictionCount; j++)
                {
                    float periodicAfflictionTimer = inc.ReadRangedSingle(afflictionPrefab.PeriodicEffects[j].MinInterval, afflictionPrefab.PeriodicEffects[j].MaxInterval, 8);
                    newPeriodicEffects.Add((afflictionPrefab.PeriodicEffects[j], periodicAfflictionTimer));
                }
                newAfflictions.Add((afflictionPrefab, afflictionStrength));
            }

            foreach (Affliction affliction in afflictions)
            {
                //deactivate afflictions that weren't included in the network message
                if (!newAfflictions.Any(a => a.afflictionPrefab == affliction.Prefab))
                {
                    affliction.Strength = 0.0f;
                }
            }

            foreach (var (afflictionPrefab, strength) in newAfflictions)
            {
                Affliction existingAffliction = afflictions.Find(a => a.Prefab == afflictionPrefab);
                if (existingAffliction == null)
                {
                    existingAffliction = afflictionPrefab.Instantiate(strength);
                    afflictions.Add(existingAffliction);
                }
                existingAffliction.SetStrength(strength);
                if (existingAffliction == stunAffliction)
                {
                    Character.SetStun(existingAffliction.Strength, true, true);
                }
                foreach (var periodicEffect in newPeriodicEffects)
                {
                    if (!existingAffliction.Prefab.PeriodicEffects.Contains(periodicEffect.effect)) { continue; }
                    //timer has wrapped around, apply the effect
                    if (periodicEffect.timer - existingAffliction.PeriodicEffectTimers[periodicEffect.effect] > periodicEffect.effect.MinInterval / 2)
                    {
                        existingAffliction.PeriodicEffectTimers[periodicEffect.effect] = periodicEffect.timer;
                        foreach (StatusEffect effect in periodicEffect.effect.StatusEffects)
                        {
                            existingAffliction.ApplyStatusEffect(ActionType.OnActive, effect, deltaTime: 1.0f, this, targetLimb: null);
                        }
                    }
                }
            }

            newLimbAfflictions.Clear();
            byte limbAfflictionCount = inc.ReadByte();
            for (int i = 0; i < limbAfflictionCount; i++)
            {
                int limbIndex = inc.ReadRangedInteger(0, limbHealths.Count - 1);
                uint afflictionID = inc.ReadUInt32();
                AfflictionPrefab afflictionPrefab = AfflictionPrefab.Prefabs.Find(p => p.UIntIdentifier == afflictionID);
                if (afflictionPrefab == null)
                {
                    DebugConsole.ThrowError("Error while reading character health data: affliction with the uint ID " + afflictionID + " not found.");
                    //read the 8 bytes for affliction strength anyway to prevent messing up reading rest of the message
                    _ = inc.ReadRangedSingle(0.0f, 100.0f, 8);
                    int _periodicAfflictionCount = inc.ReadByte();
                    for (int j = 0; j < _periodicAfflictionCount; j++)
                    {
                        _ = inc.ReadByte();
                    }
                    continue;
                }
                float afflictionStrength = inc.ReadRangedSingle(0.0f, afflictionPrefab.MaxStrength, 8);
                int periodicAfflictionCount = inc.ReadByte();
                for (int j = 0; j < periodicAfflictionCount; j++)
                {
                    float periodicAfflictionTimer = inc.ReadRangedSingle(afflictionPrefab.PeriodicEffects[j].MinInterval, afflictionPrefab.PeriodicEffects[j].MaxInterval, 8);
                    newPeriodicEffects.Add((afflictionPrefab.PeriodicEffects[j], periodicAfflictionTimer));
                }
                newLimbAfflictions.Add((limbHealths[limbIndex], afflictionPrefab, afflictionStrength));
            }

            foreach (LimbHealth limbHealth in limbHealths)
            {
                foreach (Affliction affliction in limbHealth.Afflictions)
                {
                    //deactivate afflictions that weren't included in the network message
                    if (!newLimbAfflictions.Any(a => a.limb == limbHealth && a.afflictionPrefab == affliction.Prefab))
                    {
                        affliction.Strength = 0.0f;
                    }
                }

                foreach (var (limb, afflictionPrefab, strength) in newLimbAfflictions)
                {
                    if (limb != limbHealth) { continue; }
                    Affliction existingAffliction = limbHealth.Afflictions.Find(a => a.Prefab == afflictionPrefab);
                    if (existingAffliction == null)
                    {
                        existingAffliction = afflictionPrefab.Instantiate(strength);
                        limbHealth.Afflictions.Add(existingAffliction);
                    }
                    existingAffliction.SetStrength(strength);

                    foreach (var periodicEffect in newPeriodicEffects)
                    {
                        if (!existingAffliction.Prefab.PeriodicEffects.Contains(periodicEffect.effect)) { continue; }
                        //timer has wrapped around, apply the effect
                        if (periodicEffect.timer - existingAffliction.PeriodicEffectTimers[periodicEffect.effect] > periodicEffect.effect.MinInterval / 2)
                        {
                            existingAffliction.PeriodicEffectTimers[periodicEffect.effect] = periodicEffect.timer;
                            foreach (StatusEffect effect in periodicEffect.effect.StatusEffects)
                            {
                                Limb targetLimb = Character.AnimController.Limbs.FirstOrDefault(l => l.HealthIndex == limbHealths.IndexOf(limb));
                                existingAffliction.ApplyStatusEffect(ActionType.OnActive, effect, deltaTime: 1.0f, this, targetLimb: targetLimb);
                            }
                        }
                    }
                }
            }

            CalculateVitality();
            DisplayedVitality = Vitality;
        }

        partial void UpdateLimbAfflictionOverlays()
        {
            foreach (Limb limb in Character.AnimController.Limbs)
            {
                if (limb.HealthIndex < 0 || limb.HealthIndex >= limbHealths.Count) { continue; }

                limb.BurnOverlayStrength = 0.0f;
                limb.DamageOverlayStrength = 0.0f;
                if (limbHealths[limb.HealthIndex].Afflictions.Count == 0) continue;
                foreach (Affliction a in limbHealths[limb.HealthIndex].Afflictions)
                {
                    limb.BurnOverlayStrength += a.Strength / a.Prefab.MaxStrength * a.Prefab.BurnOverlayAlpha;
                    limb.DamageOverlayStrength += a.Strength / a.Prefab.MaxStrength * a.Prefab.DamageOverlayAlpha;
                }
                limb.BurnOverlayStrength /= limbHealths[limb.HealthIndex].Afflictions.Count;
                limb.DamageOverlayStrength /= limbHealths[limb.HealthIndex].Afflictions.Count;
            }
        }

        partial void RemoveProjSpecific()
        {
            foreach (LimbHealth limbHealth in limbHealths)
            {
                if (limbHealth.IndicatorSprite != null)
                {
                    limbHealth.IndicatorSprite.Remove();
                    limbHealth.IndicatorSprite = null;
                }
            }

            medUIExtra?.Remove();
            medUIExtra = null;

            limbIndicatorOverlay?.Remove();
            limbIndicatorOverlay = null;
        }
    }
}

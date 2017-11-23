using GTA;
using GTA.Native;
using GTA.Math;
using NativeUI;
using NAudio.Wave;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Starman {

    public class Main : Script {

        private static string[] starmanThemes = new string[] { "smb", "smw", "smk", "sm64", "sm64m", "mk64", "mksc", "mkdd", "mkds", "spm", "mkwii", "nsmbwii", "msm", "smg2", "mk7", "sm3dl", "mk8", "smw2", "smrpg", "smbd", "smas", "sma4", "sma2", "sma", "mss", "msma" };

        private ScriptSettings ss;
        private Player player = Game.Player;
        private AudioFileReader audioReader = null;
        private WaveOutEvent waveOut = null;
        private bool activated = false;
        private Random rnd = new Random();
        private DateTime dateTimeThatStarmanWasInitiated;
        private string previousTheme;
        private DateTime janFirst1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc); // this acts as an initial/unset value for dateTimeThatStarmanWasInitiated
        private TimerBarPool tbPool = new TimerBarPool();
        private BarTimerBar btb = null;
        private TimeSpan elapsedTime;
        private bool hasReloaded = false;
        private float volume = 0.4f;

        // modify these
        private int starmanTime = 20; // seconds, I recommend: 0 < x <= 20
        private int fadeOutTime = 3; // seconds, I recommend: 0 < x <= 5
        private float destructionRadius = 6.0f; // game units

        public Main() {
            Tick += onTick;
            KeyUp += onKeyUp;

            dateTimeThatStarmanWasInitiated = janFirst1970; // now we set the value

            ss = ScriptSettings.Load(@"scripts\Starman.ini");
            if(ss.GetValue("Settings", "JumpBoost") == null) {
                ss.SetValue("Settings", "JumpBoost", "true");
            }
            if(ss.GetValue("Settings", "Key") == null) {
                ss.SetValue("Settings", "Key", "105");
            }
            if(ss.GetValue("Settings", "VehiclePower") == null) {
                ss.SetValue("Settings", "VehiclePower", "20");
            }
            if(ss.GetValue("Settings", "Volume") == null) {
                ss.SetValue("Settings", "Volume", "0.4");
            }
            ss.Save();
        }

        private void onTick(object sender, EventArgs e) {
            if(activated && !hasReloaded) {
                // how long has starman been activated?
                if(dateTimeThatStarmanWasInitiated != janFirst1970) {
                    elapsedTime = DateTime.Now - dateTimeThatStarmanWasInitiated;
                }

                // update the progress bar
                if(btb != null && elapsedTime.TotalSeconds <= starmanTime) {
                    btb.Percentage = 1 - ((float)elapsedTime.TotalSeconds / starmanTime);
                    float hue = ((float)elapsedTime.TotalSeconds / starmanTime);
                    btb.ForegroundColor = ExtendedColor.HSL2RGB(hue, 1, 0.5);
                    btb.BackgroundColor = ExtendedColor.HSL2RGB(hue, 1, 0.3);
                }

                tbPool.Draw();

                if(elapsedTime.TotalSeconds > starmanTime) { // starman finished
                    EndStarman();
                } else if(elapsedTime.TotalSeconds <= starmanTime) { // starman is still running
                    // particles execute every 0.5 seconds
                    if(Math.Round(elapsedTime.TotalSeconds, 2) % 0.5d == 0) {
                        if(player.Character.IsInVehicle()) {
                            ParticleOnEntity(player.Character.CurrentVehicle, "scr_rcbarry1", "scr_alien_teleport", 1.0f);
                        } else {
                            ParticleOnEntity(player.Character, "scr_rcbarry1", "scr_alien_teleport", 1.0f);
                        }
                    }

                    // is the player in a vehicle?
                    if(player.Character.IsInVehicle()) {
                        // infinite health
                        Vehicle vehicle = player.Character.CurrentVehicle;
                        vehicle.CanBeVisiblyDamaged = false;
                        vehicle.CanTiresBurst = false;
                        vehicle.CanWheelsBreak = false;
                        vehicle.EngineCanDegrade = false;
                        vehicle.IsBulletProof = true;
                        vehicle.IsExplosionProof = true;
                        vehicle.IsFireProof = true;

                        // explode on contact
                        Vehicle[] vehicles = World.GetNearbyVehicles(vehicle.Position, destructionRadius);
                        if(vehicles.Length > 0) {
                            foreach(Vehicle v in vehicles) {
                                if(v != player.Character.CurrentVehicle) {
                                    if(player.Character.CurrentVehicle.IsTouching(v)) {
                                        v.Explode();
                                    }
                                }
                            }
                        }
                    } else {
                        // super jump
                        if(bool.Parse(ss.GetValue("Settings", "JumpBoost"))) {
                            player.SetSuperJumpThisFrame();
                        }

                        // kill closeby peds
                        Ped[] peds = World.GetNearbyPeds(player.Character.Position, destructionRadius - 4.5f);
                        if(peds.Length > 0) {
                            foreach(Ped p in peds) {
                                if(p != player.Character) {
                                    p.Kill();
                                }
                            }
                        }
                    }
                }
            }
            // if the mod has been reloaded
            if(hasReloaded && activated) {
                Wait(3000);
                hasReloaded = false;
                UI.Notify("The cooldown for the Starman mod is over.");
                EndStarman();
            }
        }

        private void onKeyUp(object sender, KeyEventArgs e) {
            if(e.KeyValue == int.Parse(ss.GetValue("Settings", "Key"))) {
                if(!hasReloaded && !activated) {
                    dateTimeThatStarmanWasInitiated = DateTime.Now;
                    StartStarman();
                } else if(hasReloaded) {
                    UI.Notify("There is a 1-5 second cooldown for the Starman mod after pressing Insert.");
                }
            }
            if(e.KeyCode == Keys.Insert && activated) { // if the mods are reloaded
                hasReloaded = true;
            }
        }

        #region Starman
        private void StartStarman() {
            string chosenTheme = starmanThemes[rnd.Next(1, starmanThemes.Length)];
            while(chosenTheme == previousTheme) {
                chosenTheme = starmanThemes[rnd.Next(1, starmanThemes.Length)];
            }
            previousTheme = chosenTheme;

            // get the settings when Starman is activated
            ScriptSettings tss = ScriptSettings.Load(@"scripts\Starman.ini");
            volume = float.Parse(tss.GetValue("Settings", "Volume", "0.4"));
            tss = null;

            audioReader = new AudioFileReader("scripts/starman/" + chosenTheme + ".mp3");
            audioReader.Volume = volume;
            DelayFadeOutSampleProvider fadeOut = new DelayFadeOutSampleProvider(audioReader);
            fadeOut.BeginFadeOut((starmanTime * 1000) - (fadeOutTime * 1000), fadeOutTime * 1000);
            waveOut = new WaveOutEvent();
            waveOut.PlaybackStopped += waveOut_PlaybackStopped;
            waveOut.Init(fadeOut);
            waveOut.Play();

            btb = new BarTimerBar("STARMAN POWER");
            btb.Percentage = 1;
            btb.ForegroundColor = ExtendedColor.HSL2RGB(0, 1, 0.5);
            btb.BackgroundColor = ExtendedColor.HSL2RGB(0, 1, 0.3);
            tbPool.Add(btb);

            activated = true;
            SetInvulnerability(activated);
        }

        private void EndStarman() {
            activated = false;
            SetInvulnerability(activated);

            if(btb != null) {
                tbPool.Remove(btb);
                btb = null;
            }

            dateTimeThatStarmanWasInitiated = janFirst1970;
        }
        #endregion

        private void waveOut_PlaybackStopped(object sender, StoppedEventArgs e) {
            if(activated && audioReader != null && waveOut != null) {
                audioReader.Dispose();
                waveOut.Dispose();
                audioReader = null;
                waveOut = null;
            }
        }

        private void SetInvulnerability(bool i) {
            if(player != null) {
                if(player.Character != null) {
                    player.Character.CanBeDraggedOutOfVehicle = !i;
                    player.Character.CanBeShotInVehicle = !i;
                    player.Character.CanRagdoll = !i;
                    player.Character.CanSufferCriticalHits = !i;
                    player.Character.IsBulletProof = i;
                    player.Character.IsExplosionProof = i;
                    player.Character.IsFireProof = i;
                }
                SetInvincible(i);
                SetSprint(i ? 1.49f : 1.0f);
                SetSwim(i ? 1.49f : 1.0f);
            }

            if(player.Character.IsInVehicle()) {
                Vehicle vehicle = player.Character.CurrentVehicle;
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, vehicle, i);
                vehicle.CanBeVisiblyDamaged = !i;
                vehicle.CanTiresBurst = !i;
                vehicle.CanWheelsBreak = !i;
                vehicle.EngineCanDegrade = !i;
                vehicle.EnginePowerMultiplier = (i ? int.Parse(ss.GetValue("Settings", "VehiclePower")) : 1);
                vehicle.IsBulletProof = i;
                vehicle.IsExplosionProof = i;
                vehicle.IsFireProof = !i;
            }
        }

        private void SetInvincible(bool i) {
            Function.Call(Hash.SET_PLAYER_INVINCIBLE, player, i);
        }

        private void SetSprint(float s) {
            Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, player, s);
        }

        private void SetSwim(float s) {
            Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, player, s);
        }

        private void ParticleOnEntity(Entity entity, string argOneTwo, string argThree, float scale) {
            Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, new InputArgument[] { argOneTwo });
            Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, new InputArgument[] { argOneTwo });
            Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_ON_ENTITY, argThree, entity, 0, 0, 0, 0, 0, 0, scale, false, false, false);
        }
    }
}

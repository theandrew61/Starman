using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using NativeUI;
using NAudio.Wave;
using GTA.Math;

namespace Starman {

    public class Main : Script {

        private static string[] starmen = new string[] { "smb", "smw", "smk", "sm64", "sm64m", "mk64", "mksc", "mkdd", "mkds", "spm", "mkwii", "nsmbwii", "msm", "smg2", "mk7", "sm3dl", "mk8" };

        private ScriptSettings ss;
        private Player player = Game.Player;
        private AudioFileReader audioReader;
        private WaveOutEvent waveOut;
        private bool activated = false;
        private Random rnd = new Random();
        private DateTime janFirst1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private DateTime dateTimeThatStarmanWasInitiated;
        private TimerBarPool tbPool = new TimerBarPool();
        private BarTimerBar btb;
        private TimeSpan elapsedTime;

        // modify these
        private int starmanTime = 20; // seconds
        private int fadeOutTime = 3; // seconds
        private float destructionRadius = 6.0f; // game units

        public Main() {
            Tick += onTick;
            KeyUp += onKeyUp;

            dateTimeThatStarmanWasInitiated = janFirst1970;

            ss = ScriptSettings.Load(@"scripts\Starman.ini");
            if(ss.GetValue("Settings", "Key") == null) {
                ss.SetValue("Settings", "Key", "105");
                ss.Save();
            }
        }

        private void onTick(object sender, EventArgs e) {
            if(activated) {
                // how long has starman been activated?
                if(dateTimeThatStarmanWasInitiated != janFirst1970) {
                    elapsedTime = DateTime.Now - dateTimeThatStarmanWasInitiated;
                }

                // update the progress bar
                if(btb != null && elapsedTime.TotalSeconds <= starmanTime) {
                    btb.Percentage = 1 - ((float)elapsedTime.TotalSeconds / starmanTime);
                    btb.Label = "STARMAN POWER";
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
                        vehicle.EnginePowerMultiplier = 10;
                        vehicle.Health = 1000;
                        vehicle.IsFireProof = true;

                        // explode on contact
                        Vehicle[] vehicles = World.GetNearbyVehicles(player.Character.Position, destructionRadius);
                        if(vehicles.Length > 0) {
                            foreach(Vehicle v in vehicles) {
                                if(v != player.Character.CurrentVehicle) {
                                    if(v.ClassType != VehicleClass.Boats && v.ClassType != VehicleClass.Helicopters && v.ClassType != VehicleClass.Planes && v.ClassType != VehicleClass.Trains) {
                                        if(player.Character.CurrentVehicle.IsTouching(v)) {
                                            v.Explode();
                                        }
                                    }
                                }
                            }
                        }
                    } else {
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
        }

        private void onKeyUp(object sender, KeyEventArgs e) {
            if(e.KeyValue == int.Parse(ss.GetValue("Settings", "Key")) && !activated) {
                dateTimeThatStarmanWasInitiated = DateTime.Now;
                StartStarman();
            }
        }

        #region Starman
        private void StartStarman() {
            audioReader = new AudioFileReader("scripts/starman/" + starmen[rnd.Next(1, starmen.Length)] + ".mp3");
            audioReader.Volume = 0.4f;
            DelayFadeOutSampleProvider fadeOut = new DelayFadeOutSampleProvider(audioReader);
            fadeOut.BeginFadeOut(20000 - (fadeOutTime * 1000), fadeOutTime * 1000);
            waveOut = new WaveOutEvent();
            waveOut.PlaybackStopped += waveOut_PlaybackStopped;
            waveOut.Init(fadeOut);
            waveOut.Play();

            btb = new BarTimerBar("STARMAN TIME");
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
            }
        }

        private void SetInvulnerability(bool i) {
            player.Character.CanBeDraggedOutOfVehicle = !i;
            player.Character.CanBeShotInVehicle = !i;
            player.Character.CanRagdoll = !i;
            player.Character.CanSufferCriticalHits = !i;
            player.Character.IsBulletProof = i;
            player.Character.IsExplosionProof = i;
            player.Character.IsFireProof = i;
            SetInvincible(i);
            SetSprint(i ? 1.49f : 1.0f);
            SetSwim(i ? 1.49f : 1.0f);

            if(player.Character.IsInVehicle()) {
                Vehicle vehicle = player.Character.CurrentVehicle;
                vehicle.Health = 1000;
                vehicle.CanBeVisiblyDamaged = !i;
                vehicle.CanTiresBurst = !i;
                vehicle.CanWheelsBreak = !i;
                vehicle.EngineCanDegrade = !i;
                vehicle.EnginePowerMultiplier = i ? 10 : 1;
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
            // Function.Call<int>(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, argThree, entity.Position.X, entity.Position.Y, entity.Position.Z, 0, 0, 0, scale, 0, 0, 0);
        }

        private void Print(string text, int time = 2500) {
            Function.Call(Hash._0xB87A37EEB7FAA67D, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, text);
            Function.Call(Hash._0x9D77056A530643F6, time, 1);
        }
    }
}

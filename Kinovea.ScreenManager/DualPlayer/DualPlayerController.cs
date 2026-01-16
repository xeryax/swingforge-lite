using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using Kinovea.Services;
using Kinovea.PoseDetection;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Synchronization logic.
    /// </summary>
    public class DualPlayerController
    {
        #region Events
        public event EventHandler ExportImageAsked;
        public event EventHandler ExportVideoAsked;
        #endregion

        #region Properties
        public CommonControlsPlayers View
        {
            get { return view; }
        }
        public bool Active
        {
            get { return active; }
        }
        public bool DualSaveInProgress
        {
            get { return dualSaveInProgress; }
            set { dualSaveInProgress = value; }
        }

        public CommonTimeline CommonTimeline
        {
            get { return commonTimeline; }
        }
        #endregion

        #region Members
        private bool active;
        private bool synching;
        private bool dynamicSynching;
        private bool dualSaveInProgress;

        private CommonTimeline commonTimeline = new CommonTimeline();   
        private long currentTime;   // current time in common timeline, in microseconds.
        private CommonControlsPlayers view = new CommonControlsPlayers();
        private List<PlayerScreen> players = new List<PlayerScreen>();
        private int resyncOperations = 0;
        private int maxResyncOperations = 1;
        private HotkeyCommand[] hotkeys;

        // If two videos have creation dates within this many seconds we consider them part 
        // of the same dual recording and start them together in the replay watcher context.
        private const int spanDualRecording = 5;

        // 3D pose triangulation
        private StereoTriangulator triangulator;
        private PoseFrameMatcher frameMatcher;
        private List<Pose3D> poses3D; // Legacy - kept for backward compatibility
        private Pose3DCache pose3DCache; // Unified 3D cache keyed by frame number
        private string lastTriangulationDiagnostics; // Last diagnostic breakdown for debug display
        private long lastTriangulationFrame; // Frame number of last triangulation attempt
                                                 
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructor
        public DualPlayerController()
        {
            view.Dock = DockStyle.Fill;

            view.PlayToggled += CCtrl_PlayToggled;
            view.GotoFirst += CCtrl_GotoFirst;
            view.GotoPrev += CCtrl_GotoPrev;
            view.GotoPrevKeyframe += CCtrl_GotoPrevKeyframe;
            view.GotoNext += CCtrl_GotoNext;
            view.GotoLast += CCtrl_GotoLast;
            view.GotoNextKeyframe += CCtrl_GotoNextKeyframe;
            view.GotoSync += CCtrl_GotoSync;
            view.AddKeyframe += CCtrl_AddKeyframe;
            view.SyncAsked += CCtrl_SyncAsked;
            view.MergeAsked += CCtrl_MergeAsked;
            view.PositionChanged += CCtrl_PositionChanged;
            view.ExportImageAsked += (s, e) => ExportImageAsked?.Invoke(s, e);
            view.ExportvideoAsked += (s, e) => ExportVideoAsked?.Invoke(s, e);

            hotkeys = HotkeySettingsManager.LoadHotkeys("DualPlayer");
        }
        #endregion

        #region Public methods
        public void ScreenListChanged(List<AbstractScreen> screenList)
        {
            if (screenList.Count == 2 && screenList[0] is PlayerScreen && screenList[1] is PlayerScreen)
                Enter(screenList);
            else
                Exit();
        }
        public void RefreshUICulture()
        {
            view.RefreshUICulture();

            if (synching)
                InitializeSync();
        }
        public void UpdateTrkFrame(long position)
        {
            view.UpdateCurrentPosition(position);
        }
        public void Pause()
        {
            if (!active || !synching)
                return;

            dynamicSynching = false;

            if (view.Playing)
                view.Pause();

            if (players[0].IsPlaying)
                players[0].view.OnButtonPlay();

            if (players[1].IsPlaying)
                players[1].view.OnButtonPlay();
        }
        public void StopMerge()
        {
            view.StopMerge();
        }
        public void SwapSync()
        {
            if (!synching)
                return;

            PlayerScreen temp = players[0];
            players[0] = players[1];
            players[1] = temp;

            UpdateHairLines();
        }
        /// <summary>
        /// Get the current frames from both players as OpenCV Mat objects for stereo calibration.
        /// </summary>
        /// <returns>Tuple of (frameA, frameB) or (null, null) if not available</returns>
        public (Mat frameA, Mat frameB) GetCurrentFrames()
        {
            if (!active || players.Count < 2)
                return (null, null);

            try
            {
                Bitmap bmpA = players[0].GetCurrentImage();
                Bitmap bmpB = players[1].GetCurrentImage();

                if (bmpA == null || bmpB == null)
                    return (null, null);

                Mat frameA = BitmapConverter.ToMat(bmpA);
                Mat frameB = BitmapConverter.ToMat(bmpB);

                return (frameA, frameB);
            }
            catch
            {
                return (null, null);
            }
        }

        public void CommitLaunchSettings()
        {
            if (!active)
                return;

            if (!players[0].Full || !players[1].Full)
                return;

            synching = true;
            players[0].Synched = true;
            players[1].Synched = true;

            InitializeSync();

            // Handle dual replay.
            // The screens know they are in the context of a dual replay and have ignored the auto-replay flag,
            // to avoid one video starting before the other. They wait to be started by the dual playback.
            if (!players[0].IsReplayWatcher || !players[1].IsReplayWatcher || !File.Exists(players[0].FilePath) || !File.Exists(players[1].FilePath))
                return;

            // Check that the creation dates are close enough to be considered part of the same recording.
            DateTime creation1 = File.GetCreationTime(players[0].FilePath);
            DateTime creation2 = File.GetCreationTime(players[1].FilePath);
            double span = Math.Abs((creation1 - creation2).TotalSeconds);
            if (span < spanDualRecording)
            {
                resyncOperations = 0;
                view.Play();
                dynamicSynching = true;
                EnsureBothPlaying();
            }
            else
            {
                log.DebugFormat("Dual replay detected but the videos are not considered part of the same recording. {0}s offset", span);
            }
        }
        #endregion

        #region Players event handlers
        private void Player_PauseAsked(object sender, EventArgs e)
        {
            if (active && synching && view.Playing)
                Pause();
        }

        private void Player_PlayStarted(object sender, EventArgs e)
        {
            // If both players are playing, we must activate the dynamic synching, even if they were started independently. 
            // This way they will continue playing on the next loop, this time in sync.
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null)
                return;

            PlayerScreen otherPlayer = GetOtherPlayer(player);

            if (player.IsPlaying && otherPlayer.IsPlaying && !view.Playing)
            {
                // Immediately force synchronization.
                // This may not fully register for automatically started players, see `resyncOperations`.
                commonTimeline.Initialize(players[0], players[1]);
                view.UpdateSyncPosition(commonTimeline.GetCommonTime(players[0], players[0].LocalTimeOriginPhysical));

                AlignPlayers(false);

                resyncOperations = 0;
                view.Play();
                dynamicSynching = true;
                EnsureBothPlaying();
            }
        }

        private void Player_SpeedChanged(object sender, EventArgs e)
        {
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null || !PreferencesManager.PlayerPreferences.SyncLockSpeed)
                return;

            GetOtherPlayer(player).RealtimePercentage = player.RealtimePercentage;
        }

        private void Player_TimeOriginChanged(object sender, EventArgs e)
        {
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null)
                return;

            // Reinit synchronization.
            commonTimeline.Initialize(players[0], players[1]);
            currentTime = commonTimeline.GetCommonTime(player, player.LocalTime);
            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            view.UpdateSyncPosition(currentTime);
            UpdateHairLines();
        }

        private void Player_HighSpeedFactorChanged(object sender, EventArgs e)
        {
            if (!active || !synching)
                return;

            if (PreferencesManager.PlayerPreferences.SyncLockSpeed)
            {
                double percentage = Math.Min(players[0].RealtimePercentage, players[1].RealtimePercentage);
                players[0].RealtimePercentage = percentage;
                players[1].RealtimePercentage = percentage;
            }

            // Synchronization must be reinitialized.
            commonTimeline.Initialize(players[0], players[1]);

            // TODO: Check if current time is still in bounds.
            currentTime = Math.Min(currentTime, commonTimeline.GetCommonTime(players[0], players[0].LocalTime));

            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            view.UpdateSyncPosition(currentTime); 
        }

        private void Player_ImageChanged(object sender, EventArgs<Bitmap> e)
        {
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null)
                return;

            PlayerScreen otherPlayer = GetOtherPlayer(player);

            if (dynamicSynching)
            {
                if (player.IsPlaying)
                {
                    //log.DebugFormat("Received image from [{0}] ({1}).", GetPlayerIndex(player), player.LocalTime / 1000);
                    currentTime = commonTimeline.GetCommonTime(player, player.LocalTime);

                    if (otherPlayer.IsPlaying && resyncOperations < maxResyncOperations)
                    {
                        //----------------------------------------------------------------------------------
                        // Test for desync. 
                        // This is not the primary synchronization mechanism, videos should synchronize naturally from being started
                        // at the right time, based on their time origin and the fact that their playback rate relative to real time should match.
                        //
                        // However, for videos started automatically, we can't guarantee that one isn't ahead of the other.
                        // We allow ourselves one attempt at resync here.
                        // This kind of resync can easily misfire and cause judder so it's only used very sparingly.
                        //----------------------------------------------------------------------------------
                        long otherTime = commonTimeline.GetCommonTime(otherPlayer, otherPlayer.LocalTime);
                        long divergence = Math.Abs(currentTime - otherTime);
                        long frameTime = Math.Max(player.LocalFrameTime, otherPlayer.LocalFrameTime);
                        if (divergence > frameTime)
                        {
                            resyncOperations++;

                            log.WarnFormat("Synchronization divergence: [{0}]@{1} vs [{2}]@{3}. Resynchronizing.", 
                                GetPlayerIndex(player), currentTime / 1000, GetPlayerIndex(otherPlayer), otherTime / 1000);

                            AlignPlayers(true);
                        }
                    }

                    EnsureBothPlaying();
                }
                else if (!otherPlayer.IsPlaying)
                {
                    // Both players have completed a loop and are waiting.
                    currentTime = 0;
                    EnsureBothPlaying();
                }
            }

            UpdateHairLines();
            
            // Perform real-time triangulation when frames change
            if (synching && !view.Merging)
            {
                TriangulateCurrentFrame();
            }
            else if (synching)
            {
                // Even if not triangulating, update debug panel with current frame from cache
                Services.NotificationCenter.RaisePose3DUpdated(this);
            }
            
            if (!view.Merging || e.Value == null)
                return;

            otherPlayer.SetSyncMergeImage(e.Value, !dualSaveInProgress);
        }
        #endregion

        #region View event handlers
        private void CCtrl_PlayToggled(object sender, EventArgs e)
        {
            if (synching)
            {
                AlignPlayers(false);

                dynamicSynching = view.Playing;
                if (dynamicSynching)
                {
                    resyncOperations = 0;
                    EnsureBothPlaying();
                }
            }

            // Propagate the stop call to screens.
            if (!view.Playing)
                Pause();
        }
        private void CCtrl_GotoFirst(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                currentTime = 0;
                GotoTime(currentTime, true);
                UpdateTrkFrame(currentTime);
            }
            else
            {
                foreach (PlayerScreen player in players)
                    player.view.buttonGotoFirst_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_GotoPrev(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                if (currentTime > 0)
                {
                    currentTime -= commonTimeline.FrameTime;

                    GotoTime(currentTime, true);
                    UpdateTrkFrame(currentTime);
                }
            }
            else
            {
                foreach (PlayerScreen screen in players)
                    screen.view.buttonGotoPrevious_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_GotoNext(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                if (currentTime < commonTimeline.LastTime)
                {
                    currentTime += commonTimeline.FrameTime;
                    
                    GotoTime(currentTime, true);
                    UpdateTrkFrame(currentTime);
                }
            }
            else
            {
                foreach (PlayerScreen player in players)
                    player.view.buttonGotoNext_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_GotoLast(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                currentTime = commonTimeline.LastTime;
                GotoTime(currentTime, true);
                UpdateTrkFrame(currentTime);
            }
            else
            {
                foreach (PlayerScreen player in players)
                    player.view.buttonGotoLast_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_SyncAsked(object sender, EventArgs e)
        {
            if (!synching)
                return;

            SetSyncPoint(false);
            GotoTime(currentTime, true);
        }
        private void CCtrl_MergeAsked(object sender, EventArgs e)
        {
            if (!synching)
                return;

            log.Debug(String.Format("SyncMerge videos is now {0}", view.Merging.ToString()));

            // This will also do a full refresh, and trigger back Player_ImageChanged().
            players[0].SyncMerge = view.Merging;
            players[1].SyncMerge = view.Merging;
        }
        private void CCtrl_PositionChanged(object sender, TimeEventArgs e)
        {
            if (!synching)
                return;

            Pause();
            
            currentTime = e.Time;
            GotoTime(currentTime, true);
        }
        
        private void CCtrl_GotoPrevKeyframe(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;

            players[0].GotoPrevKeyframe();
            players[1].GotoPrevKeyframe();
        }
        private void CCtrl_GotoNextKeyframe(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;

            players[0].GotoNextKeyframe();
            players[1].GotoNextKeyframe();
        }
        private void CCtrl_AddKeyframe(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;

            players[0].AddKeyframe();
            players[1].AddKeyframe();
        }
        private void CCtrl_GotoSync(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;
            
            currentTime = commonTimeline.GetCommonTime(players[0], players[0].LocalTimeOriginPhysical);
            GotoTime(currentTime, true);
            UpdateTrkFrame(currentTime);
        }
        
        #endregion

        #region Entering/Exiting dual player management
        private void Enter(List<AbstractScreen> screenList)
        {
            Exit();

            players.Clear();
            players.Add((PlayerScreen)screenList[0]);
            players.Add((PlayerScreen)screenList[1]);

            foreach (PlayerScreen player in players)
                AddEventHandlers(player);

            active = true;
        }
        private void Exit()
        {
            // Save 3D cache before exiting
            if (active)
            {
                Save3DCache();
            }

            synching = false;
            dynamicSynching = false;

            if (active)
            {
                foreach (PlayerScreen player in players)
                    RemoveEventHandlers(player);

                players.Clear();
            }

            active = false;
        }
        private void AddEventHandlers(PlayerScreen player)
        {
            player.PlayStarted += Player_PlayStarted;
            player.PauseAsked += Player_PauseAsked;
            player.SpeedChanged += Player_SpeedChanged;
            player.HighSpeedFactorChanged += Player_HighSpeedFactorChanged;
            player.TimeOriginChanged += Player_TimeOriginChanged;
            player.ImageChanged += Player_ImageChanged;
        }
        private void RemoveEventHandlers(PlayerScreen player)
        {
            player.PlayStarted -= Player_PlayStarted;
            player.PauseAsked -= Player_PauseAsked;
            player.SpeedChanged -= Player_SpeedChanged;
            player.HighSpeedFactorChanged -= Player_HighSpeedFactorChanged;
            player.ImageChanged -= Player_ImageChanged;
        }
        #endregion

        public void ExecuteDualCommand(HotkeyCommand playerCommand)
        {
            // A player has detected that a hotkey it received should actually be handled at the dual player level.
            // At that point there is still two options, either it's a true dual player command,
            // something normally bound to controls in the common controls,
            // or it's a multiplexed command, a command that should simply be forwarded to each player.

            HotkeyCommand dualCommand = hotkeys.FirstOrDefault(hk => hk != null && hk.KeyData == playerCommand.KeyData);
            if (dualCommand == null)
                return;

            DualPlayerCommands command = (DualPlayerCommands)dualCommand.CommandCode;

            switch(command)
            {
                case DualPlayerCommands.GotoPreviousKeyframe:
                case DualPlayerCommands.GotoNextKeyframe:
                case DualPlayerCommands.GotoSyncPoint:
                case DualPlayerCommands.AddKeyframe:
                    players[0].ExecuteScreenCommand(playerCommand.CommandCode);
                    players[1].ExecuteScreenCommand(playerCommand.CommandCode);
                    break;

                default:
                    view.ExecuteDualCommand(dualCommand.CommandCode);
                    break;
            }
        }


        #region Synchronization
        public void ResetSync()
        {
            if (!active)
                return;
            
            log.DebugFormat("Re-initializing dual player synchronization");

            synching = false;
            dynamicSynching = false;
            Pause();

            if (!players[0].Full || !players[1].Full)
                return;

            synching = true;
            players[0].Synched = true;
            players[1].Synched = true;

            if (PreferencesManager.PlayerPreferences.SyncLockSpeed)
            {
                double percentage = Math.Min(players[0].RealtimePercentage, players[1].RealtimePercentage);
                players[0].RealtimePercentage = percentage;
                players[1].RealtimePercentage = percentage;
            }

            InitializeSync();

            players[0].SyncMerge = false;
            players[1].SyncMerge = false;
            StopMerge();

            GotoTime(currentTime, true);
        }

        private void InitializeSync()
        {
            commonTimeline.Initialize(players[0], players[1]);
            currentTime = 0;
            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            view.UpdateSyncPosition(commonTimeline.GetCommonTime(players[0], players[0].LocalTimeOriginPhysical));
            UpdateHairLines();

            // Load 3D cache if both videos are loaded
            Load3DCache();

            log.Debug("Synchronization initialized.");
        }

        private void SetSyncPoint(bool intervalOnly)
        {
            log.DebugFormat("Resetting time origins. [0]:{0}, [1]{1}", players[0].LocalTime, players[1].LocalTime);
            players[0].LocalTimeOriginPhysical = players[0].LocalTime;
            players[1].LocalTimeOriginPhysical = players[1].LocalTime;

            commonTimeline.Initialize(players[0], players[1]);
            currentTime = commonTimeline.GetCommonTime(players[0], players[0].LocalTime);
            
            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            view.UpdateSyncPosition(currentTime); 
        }

        private void GotoTime(long commonTime, bool allowUIUpdate)
        {
            GotoTime(players[0], commonTime, allowUIUpdate);
            GotoTime(players[1], commonTime, allowUIUpdate);

            UpdateHairLines();
            
            // Perform real-time triangulation for current frame
            TriangulateCurrentFrame();
            
            // Always notify debug panel (even if triangulation didn't happen, we still want to show current frame from cache)
            if (synching && pose3DCache != null)
            {
                Services.NotificationCenter.RaisePose3DUpdated(this);
            }
        }
        
        private void GotoTime(PlayerScreen player, long commonTime, bool allowUIUpdate)
        {
            long localTime = commonTimeline.GetLocalTime(player, commonTime);
            localTime = Math.Max(0, localTime);

            if (player.LocalTime != localTime)
                player.GotoTime(localTime, allowUIUpdate);
        }

        private void UpdateHairLines()
        {
            long leftTime = commonTimeline.GetCommonTime(players[0], players[0].LocalTime);
            long rightTime = commonTimeline.GetCommonTime(players[1], players[1].LocalTime);

            view.UpdateHairline(leftTime, true);
            view.UpdateHairline(rightTime, false);
        }
        
        /// <summary>
        /// Force both players to align on a common time.
        /// Used if players may have moved independently from the common tracker.
        /// Should not be used while playback is active.
        /// </summary>
        private void AlignPlayers(bool catchup)
        {
            long leftTime = commonTimeline.GetCommonTime(players[0], players[0].LocalTime);
            long rightTime = commonTimeline.GetCommonTime(players[1], players[1].LocalTime);

            if (catchup)
                currentTime = Math.Max(leftTime, rightTime);
            else
                currentTime = Math.Min(leftTime, rightTime);

            log.DebugFormat("Aligning players to {0}.", currentTime / 1000);
            GotoTime(currentTime, true);
        }

        private void EnsureBothPlaying()
        {
            EnsurePlaying(0);
            EnsurePlaying(1);
        }

        /// <summary>
        /// Make sure a player is playing if it needs to but does not start it if it shouldn't.
        /// </summary>
        private void EnsurePlaying(int index)
        {
            if (!players[index].IsPlaying && !commonTimeline.IsOutOfBounds(players[index], currentTime))
                players[index].EnsurePlaying();
        }

        #endregion

        private PlayerScreen GetOtherPlayer(PlayerScreen player)
        {
            return player == players[0] ? players[1] : players[0];
        }

        private int GetPlayerIndex(PlayerScreen player)
        {
            return player == players[0] ? 0 : 1;
        }

        #region 3D Pose Triangulation

        /// <summary>
        /// Get the list of triangulated 3D poses (legacy - for backward compatibility).
        /// </summary>
        public List<Pose3D> Poses3D
        {
            get { return poses3D; }
        }

        /// <summary>
        /// Check if 3D poses are available.
        /// </summary>
        public bool Has3DPoses
        {
            get { return (pose3DCache != null && pose3DCache.Poses != null && pose3DCache.Poses.Count > 0) ||
                          (poses3D != null && poses3D.Count > 0); }
        }

        /// <summary>
        /// Get the unified 3D cache.
        /// </summary>
        public Pose3DCache Pose3DCache
        {
            get { return pose3DCache; }
        }

        /// <summary>
        /// Initialize the triangulation system.
        /// </summary>
        public bool InitializeTriangulation()
        {
            triangulator = new StereoTriangulator();
            frameMatcher = new PoseFrameMatcher();

            if (!triangulator.IsReady)
            {
                log.WarnFormat("Triangulation not ready: {0}", triangulator.LastError);
                return false;
            }

            log.DebugFormat("Stereo triangulation initialized. Calibration loaded: {0}", triangulator.IsReady);
            return true;
        }

        /// <summary>
        /// Triangulate 3D poses from both player's 2D pose caches.
        /// Call this after both videos have completed pose analysis.
        /// </summary>
        public bool Triangulate3DPoses()
        {
            if (!active || players.Count < 2)
                return false;

            if (triangulator == null || !triangulator.IsReady)
            {
                if (!InitializeTriangulation())
                    return false;
            }

            // Get pose caches from both players
            var cacheA = players[0].GetPoseCache();
            var cacheB = players[1].GetPoseCache();

            if (cacheA == null || cacheB == null)
            {
                log.Warn("Cannot triangulate: one or both pose caches are null.");
                return false;
            }

            if (cacheA.Frames == null || cacheA.Frames.Count == 0 ||
                cacheB.Frames == null || cacheB.Frames.Count == 0)
            {
                log.Warn("Cannot triangulate: one or both pose caches are empty.");
                return false;
            }

            // Perform triangulation
            poses3D = frameMatcher.TriangulateAll(cacheA, cacheB, triangulator);

            log.DebugFormat("Triangulated {0} 3D poses from {1} + {2} 2D poses.",
                poses3D.Count, cacheA.Frames.Count, cacheB.Frames.Count);

            return poses3D.Count > 0;
        }

        /// <summary>
        /// Get the 3D pose for a specific frame number.
        /// </summary>
        public Pose3D GetPose3D(int frameNumber)
        {
            if (poses3D == null)
                return null;

            return poses3D.FirstOrDefault(p => p.FrameNumber == frameNumber);
        }

        /// <summary>
        /// Get the closest 3D pose by timestamp (legacy method).
        /// </summary>
        public Pose3D GetClosestPose3D(double timestampMs)
        {
            if (poses3D == null || poses3D.Count == 0)
                return null;

            Pose3D closest = null;
            double minDiff = double.MaxValue;

            foreach (var pose in poses3D)
            {
                double diff = Math.Abs(pose.TimestampMs - timestampMs);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = pose;
                }
            }

            return closest;
        }

        /// <summary>
        /// Load 3D cache for the current video pair.
        /// </summary>
        private void Load3DCache()
        {
            if (!active || players.Count < 2)
                return;

            string videoPathA = players[0].FilePath;
            string videoPathB = players[1].FilePath;

            if (string.IsNullOrEmpty(videoPathA) || string.IsNullOrEmpty(videoPathB))
                return;

            // Check if cache exists and is valid
            if (Pose3DCache.IsValid(videoPathA, videoPathB))
            {
                pose3DCache = Pose3DCache.Load(videoPathA, videoPathB);
                if (pose3DCache != null)
                {
                    log.DebugFormat("Loaded 3D cache: {0} poses", pose3DCache.Poses?.Count ?? 0);
                }
            }
            else
            {
                // Create new cache
                pose3DCache = new Pose3DCache();
                log.Debug("Created new 3D cache");
            }
        }

        /// <summary>
        /// Save 3D cache to disk.
        /// </summary>
        public void Save3DCache()
        {
            if (pose3DCache == null || !active || players.Count < 2)
                return;

            string videoPathA = players[0].FilePath;
            string videoPathB = players[1].FilePath;

            if (string.IsNullOrEmpty(videoPathA) || string.IsNullOrEmpty(videoPathB))
                return;

            try
            {
                pose3DCache.Save(videoPathA, videoPathB);
                log.DebugFormat("Saved 3D cache: {0} poses", pose3DCache.Poses?.Count ?? 0);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to save 3D cache: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Get 3D pose for the current synchronized frame.
        /// </summary>
        public Pose3D GetPose3DForCurrentFrame()
        {
            if (pose3DCache == null || pose3DCache.Poses == null)
                return null;

            // Get current frame numbers from both players (same logic as TriangulateCurrentFrame)
            var cacheA = players[0].GetPoseCache();
            var cacheB = players[1].GetPoseCache();
            if (cacheA != null && cacheB != null)
            {
                long localTimeA = commonTimeline.GetLocalTime(players[0], currentTime);
                long localTimeB = commonTimeline.GetLocalTime(players[1], currentTime);
                long frameNumberA = players[0].LocalFrameTime > 0 ? localTimeA / players[0].LocalFrameTime : 0;
                long frameNumberB = players[1].LocalFrameTime > 0 ? localTimeB / players[1].LocalFrameTime : 0;
                
                // Get the actual poses to see what frame numbers they have
                var poseA = cacheA.GetPoseForFrame(frameNumberA);
                var poseB = cacheB.GetPoseForFrame(frameNumberB);
                if (poseA != null && poseB != null)
                {
                    // Use the actual frame number from the retrieved pose (should match what we stored)
                    long actualFrameNumber = poseA.FrameNumber;
                    return pose3DCache.GetPose3D(actualFrameNumber);
                }
            }
            
            // Fallback: use common frame number
            long frameNumber = commonTimeline.FrameTime > 0 ? currentTime / commonTimeline.FrameTime : 0;
            return pose3DCache.GetPose3D(frameNumber);
        }

        /// <summary>
        /// Get current 3D statistics for display.
        /// </summary>
        public PoseStatistics GetCurrent3DStats()
        {
            var pose3D = GetPose3DForCurrentFrame();
            if (pose3D == null)
                return null;

            var stats = new PoseStatistics();
            stats.UpdateFrom3DPose(pose3D);
            return stats;
        }

        /// <summary>
        /// Get triangulation status information for debug display.
        /// </summary>
        public string GetTriangulationStatus()
        {
            if (!active || players.Count < 2)
                return "Not active or insufficient players";
            
            if (!synching)
                return "Synchronization not enabled";
            
            if (triangulator == null || !triangulator.IsReady)
                return triangulator == null ? "Triangulator not initialized" : 
                       (triangulator.LastError ?? "Triangulator not ready (check calibration)");
            
            if (pose3DCache == null)
                return "3D cache not loaded";
            
            // Get pose caches from both players
            var cacheA = players[0].GetPoseCache();
            var cacheB = players[1].GetPoseCache();
            
            if (cacheA == null || cacheB == null)
                return string.Format("Missing pose caches (A={0}, B={1})", cacheA != null, cacheB != null);
            
            // Get current frame numbers
            long localTimeA = commonTimeline.GetLocalTime(players[0], currentTime);
            long localTimeB = commonTimeline.GetLocalTime(players[1], currentTime);
            // LocalFrameTime is in microseconds per frame, localTimeA/B are in microseconds
            long frameNumberA = players[0].LocalFrameTime > 0 ? localTimeA / players[0].LocalFrameTime : 0;
            long frameNumberB = players[1].LocalFrameTime > 0 ? localTimeB / players[1].LocalFrameTime : 0;
            // FrameTime is in microseconds per frame, currentTime is in microseconds
            long commonFrameNumber = commonTimeline.FrameTime > 0 ? currentTime / commonTimeline.FrameTime : 0;
            
            var poseA = cacheA.GetPoseForFrame(frameNumberA);
            var poseB = cacheB.GetPoseForFrame(frameNumberB);
            
            if (poseA == null || poseB == null)
                return string.Format("Missing 2D poses for frame {0} (A={1}, B={2})", 
                    commonFrameNumber, poseA != null, poseB != null);
            
            // Check if pose exists in cache
            var existingPose = pose3DCache.GetPose3D(commonFrameNumber);
            if (existingPose != null)
            {
                int validCount = 0;
                foreach (var kp in existingPose.KeyPoints)
                {
                    if (kp != null && kp.IsValid && 
                        !double.IsNaN(kp.X) && !double.IsNaN(kp.Y) && !double.IsNaN(kp.Z))
                        validCount++;
                }
                return string.Format("Frame {0}: {1} valid keypoints (cached)", commonFrameNumber, validCount);
            }
            
            // Show last diagnostic details if available
            if (!string.IsNullOrEmpty(lastTriangulationDiagnostics) && lastTriangulationFrame == commonFrameNumber)
            {
                return string.Format("Triangulation failed - {0}", lastTriangulationDiagnostics);
            }
            
            return "Ready - triangulation will occur on frame change";
        }

        /// <summary>
        /// Triangulate 3D pose for the current synchronized frame.
        /// </summary>
        private void TriangulateCurrentFrame()
        {
            if (!active || players.Count < 2)
            {
                log.DebugFormat("TriangulateCurrentFrame: not active or not enough players (active={0}, players={1})", active, players.Count);
                return;
            }

            if (!synching)
            {
                log.Debug("TriangulateCurrentFrame: synching is false - synchronization must be enabled");
                return;
            }

            if (triangulator == null || !triangulator.IsReady)
            {
                if (!InitializeTriangulation())
                {
                    log.Warn("TriangulateCurrentFrame: failed to initialize triangulation");
                    return;
                }
            }

            if (pose3DCache == null)
            {
                Load3DCache();
                if (pose3DCache == null)
                {
                    log.Warn("TriangulateCurrentFrame: failed to load/create 3D cache");
                    return;
                }
            }

            // Get pose caches from both players
            var cacheA = players[0].GetPoseCache();
            var cacheB = players[1].GetPoseCache();

            if (cacheA == null || cacheB == null)
            {
                log.DebugFormat("TriangulateCurrentFrame: missing pose caches (cacheA={0}, cacheB={1})", cacheA != null, cacheB != null);
                return;
            }

            // Get current frame numbers using common timeline
            long localTimeA = commonTimeline.GetLocalTime(players[0], currentTime);
            long localTimeB = commonTimeline.GetLocalTime(players[1], currentTime);

            // Convert timestamps to frame numbers
            // LocalFrameTime is in microseconds per frame, localTimeA/B are in microseconds
            // So frame number = time / frameTime (no division by 1000 needed)
            long frameNumberA = players[0].LocalFrameTime > 0 ? localTimeA / players[0].LocalFrameTime : 0;
            long frameNumberB = players[1].LocalFrameTime > 0 ? localTimeB / players[1].LocalFrameTime : 0;

            // Get current frame number from common timeline (calculate before getting poses)
            long commonFrameNumber = currentTime / (commonTimeline.FrameTime / 1000);
            
            // Get 2D poses for current frames
            var poseA = cacheA.GetPoseForFrame(frameNumberA);
            var poseB = cacheB.GetPoseForFrame(frameNumberB);

            if (poseA == null || poseB == null)
            {
                log.DebugFormat("TriangulateCurrentFrame: missing pose data for frame {0} (poseA={1}, poseB={2}, frameA={3}, frameB={4})", 
                    commonFrameNumber, poseA != null, poseB != null, frameNumberA, frameNumberB);
                return;
            }
            
            // Diagnostic: Log frame numbers and confidence values to identify frame mismatch
            log.DebugFormat("Frame {0}: Requested frameA={1}, frameB={2}, Got poseA.frame={3}, poseB.frame={4}", 
                commonFrameNumber, frameNumberA, frameNumberB, poseA.FrameNumber, poseB.FrameNumber);
            
            // Diagnostic: Log sample confidence values from both cameras
            if (poseA.KeyPoints != null && poseB.KeyPoints != null && poseA.KeyPoints.Length >= 4 && poseB.KeyPoints.Length >= 4)
            {
                log.DebugFormat("Frame {0}: Camera A confidences: kp0={1:F3}, kp1={2:F3}, kp2={3:F3}, kp3={4:F3}", 
                    commonFrameNumber,
                    poseA.KeyPoints[0]?.Confidence ?? 0f,
                    poseA.KeyPoints[1]?.Confidence ?? 0f,
                    poseA.KeyPoints[2]?.Confidence ?? 0f,
                    poseA.KeyPoints[3]?.Confidence ?? 0f);
                log.DebugFormat("Frame {0}: Camera B confidences: kp0={1:F3}, kp1={2:F3}, kp2={3:F3}, kp3={4:F3}", 
                    commonFrameNumber,
                    poseB.KeyPoints[0]?.Confidence ?? 0f,
                    poseB.KeyPoints[1]?.Confidence ?? 0f,
                    poseB.KeyPoints[2]?.Confidence ?? 0f,
                    poseB.KeyPoints[3]?.Confidence ?? 0f);
            }

            // Use the actual frame numbers from the retrieved poses (they should match since we're using synchronized timeline)
            // But if they differ, use the average or the one from poseA
            long actualFrameNumber = poseA.FrameNumber;
            if (poseA.FrameNumber != poseB.FrameNumber)
            {
                // If frame numbers don't match, use the average (should be close)
                actualFrameNumber = (poseA.FrameNumber + poseB.FrameNumber) / 2;
                log.DebugFormat("Frame number mismatch: poseA.frame={0}, poseB.frame={1}, using average={2}", 
                    poseA.FrameNumber, poseB.FrameNumber, actualFrameNumber);
            }
            
            // Check if we already have this frame in cache (use actual frame number, not commonFrameNumber)
            var existingPose = pose3DCache.GetPose3D(actualFrameNumber);
            if (existingPose != null)
            {
                // Already triangulated - but still notify debug panel to update
                Services.NotificationCenter.RaisePose3DUpdated(this);
                return;
            }

            // Triangulate
            var pose3D = triangulator.Triangulate3DPose(poseA, poseB);
            
            // Capture LastError immediately after triangulation (before any other operations)
            string triangulationErrorDetails = triangulator != null ? (triangulator.LastError ?? "none") : "triangulator null";
            
            if (pose3D == null)
            {
                log.DebugFormat("Triangulation failed for frame {0}, LastError: {1}", commonFrameNumber, triangulationErrorDetails);
                return;
            }

            // DEBUG: Log what we received from Triangulate3DPose
            log.DebugFormat("=== DUALPLAYER RECEIVED POSE (Frame {0}) ===", commonFrameNumber);
            log.DebugFormat("pose3D.KeyPoints.Length = {0}", pose3D.KeyPoints != null ? pose3D.KeyPoints.Length : 0);
            if (pose3D.KeyPoints != null)
            {
                for (int i = 0; i < pose3D.KeyPoints.Length; i++)
                {
                    var kp = pose3D.KeyPoints[i];
                    if (kp != null)
                    {
                        log.DebugFormat("KP{0}: IsValid={1}, coords=({2:F3},{3:F3},{4:F3})",
                            i, kp.IsValid, kp.X, kp.Y, kp.Z);
                    }
                    else
                    {
                        log.DebugFormat("KP{0}: NULL", i);
                    }
                }
            }

            // Check for duplicate coordinates (all keypoints having same X,Y,Z) - indicates triangulation bug
            bool allSame = true;
            double? firstX = null, firstY = null, firstZ = null;
            int validCount = 0;
            log.DebugFormat("=== COUNTING VALID KEYPOINTS (Frame {0}) ===", commonFrameNumber);
            int index = 0;
            foreach (var kp in pose3D.KeyPoints)
            {
                bool isNull = kp == null;
                bool isValid = kp != null && kp.IsValid;
                bool hasNaN = kp != null && (double.IsNaN(kp.X) || double.IsNaN(kp.Y) || double.IsNaN(kp.Z));
                bool hasInf = kp != null && (double.IsInfinity(kp.X) || double.IsInfinity(kp.Y) || double.IsInfinity(kp.Z));
                bool passesCheck = kp != null && kp.IsValid && 
                    !double.IsNaN(kp.X) && !double.IsNaN(kp.Y) && !double.IsNaN(kp.Z) &&
                    !double.IsInfinity(kp.X) && !double.IsInfinity(kp.Y) && !double.IsInfinity(kp.Z);
                
                log.DebugFormat("COUNT KP{0}: null={1}, IsValid={2}, hasNaN={3}, hasInf={4}, passesCheck={5}, coords=({6:F3},{7:F3},{8:F3})",
                    index, isNull, isValid, hasNaN, hasInf, passesCheck,
                    kp != null ? kp.X : 0, kp != null ? kp.Y : 0, kp != null ? kp.Z : 0);
                
                if (passesCheck)
                {
                    validCount++;
                    if (firstX == null)
                    {
                        firstX = kp.X;
                        firstY = kp.Y;
                        firstZ = kp.Z;
                    }
                    else
                    {
                        if (Math.Abs(kp.X - firstX.Value) > 1e-6 || 
                            Math.Abs(kp.Y - firstY.Value) > 1e-6 || 
                            Math.Abs(kp.Z - firstZ.Value) > 1e-6)
                        {
                            allSame = false;
                            // Don't break - continue counting all valid keypoints
                            // The break was causing only the first 2 keypoints to be counted
                        }
                    }
                }
                index++;
            }
            log.DebugFormat("=== COUNTING COMPLETE: validCount={0} ===", validCount);
            
            if (allSame && validCount > 1)
            {
                log.WarnFormat("Triangulation bug detected for frame {0}: all {1} valid keypoints have identical coordinates ({2:F4}, {3:F4}, {4:F4})", 
                    commonFrameNumber, validCount, firstX ?? 0, firstY ?? 0, firstZ ?? 0);
                return; // Don't store invalid triangulation
            }
            
            if (validCount < 5)
            {
                // Calculate diagnostic details directly from the input poses
                // Count ALL keypoints based on input confidence values (same logic as StereoTriangulator)
                int zeroConfCount = 0;
                int lowConfCount = 0;
                int failedCount = 0;
                int totalKeypoints = 17; // COCO has 17 keypoints
                
                if (pose3D.KeyPoints != null && poseA.KeyPoints != null && poseB.KeyPoints != null)
                {
                    int numKeypoints = Math.Min(17, Math.Min(pose3D.KeyPoints.Length, Math.Min(poseA.KeyPoints.Length, poseB.KeyPoints.Length)));
                    for (int i = 0; i < numKeypoints; i++)
                    {
                        var kp = pose3D.KeyPoints[i];
                        var kpA = poseA.KeyPoints[i];
                        var kpB = poseB.KeyPoints[i];
                        
                        // Categorize based on INPUT confidence values (same logic as StereoTriangulator.Triangulate3DPose)
                        if (kpA == null || kpB == null || kpA.Confidence <= 0.0f || kpB.Confidence <= 0.0f)
                        {
                            zeroConfCount++;
                        }
                        else if (kpA.Confidence < 0.10f || kpB.Confidence < 0.10f)
                        {
                            lowConfCount++;
                        }
                        else
                        {
                            // Both have confidence >= 0.10
                            // Check if triangulation succeeded (valid and non-NaN)
                            bool isKeypointValid = kp != null && kp.IsValid && 
                                !double.IsNaN(kp.X) && !double.IsNaN(kp.Y) && !double.IsNaN(kp.Z) &&
                                !double.IsInfinity(kp.X) && !double.IsInfinity(kp.Y) && !double.IsInfinity(kp.Z);
                            
                            if (isKeypointValid)
                            {
                                // This is counted in validCount - don't double count
                                // (validCount already includes this)
                            }
                            else
                            {
                                // Both have good confidence, but triangulation failed
                                failedCount++;
                            }
                        }
                    }
                }
                
                // Verify counts add up: validCount + zeroConfCount + lowConfCount + failedCount should equal totalKeypoints
                int accountedFor = validCount + zeroConfCount + lowConfCount + failedCount;
                if (accountedFor != totalKeypoints)
                {
                    // Adjust failedCount to account for any discrepancy
                    failedCount = totalKeypoints - validCount - zeroConfCount - lowConfCount;
                }
                
                string diagnosticDetails = string.Format("valid={0}, zeroConf={1}, lowConf={2}, failed={3}, total={4}", 
                    validCount, zeroConfCount, lowConfCount, failedCount, totalKeypoints);
                
                // Store diagnostic details for debug panel
                lastTriangulationDiagnostics = diagnosticDetails;
                lastTriangulationFrame = commonFrameNumber;
                
                // Log more details about why triangulation failed
                int invalidCount = totalKeypoints - validCount;
                log.WarnFormat("Triangulation validation failed for frame {0}: only {1} valid (non-NaN) keypoints out of {2} total ({3} invalid). Triangulator ready: {4}, Details: {5}", 
                    commonFrameNumber, validCount, totalKeypoints, invalidCount, 
                    triangulator != null && triangulator.IsReady, diagnosticDetails);
                
                // Also log sample of which keypoints failed (first 5 invalid ones) at WARN level
                if (pose3D.KeyPoints != null && poseA.KeyPoints != null && poseB.KeyPoints != null)
                {
                    int logged = 0;
                    for (int i = 0; i < pose3D.KeyPoints.Length && logged < 5; i++)
                    {
                        var kp = pose3D.KeyPoints[i];
                        if (kp == null || !kp.IsValid)
                        {
                            var kpA = i < poseA.KeyPoints.Length ? poseA.KeyPoints[i] : null;
                            var kpB = i < poseB.KeyPoints.Length ? poseB.KeyPoints[i] : null;
                            log.DebugFormat("  Keypoint {0}: A conf={1:F2}, B conf={2:F2}, A valid={3}, B valid={4}", 
                                i, 
                                kpA != null ? kpA.Confidence : 0f, 
                                kpB != null ? kpB.Confidence : 0f,
                                kpA != null && kpA.Confidence > 0.10f,
                                kpB != null && kpB.Confidence > 0.10f);
                            logged++;
                        }
                    }
                }
                return;
            }
            
            if (!triangulator.ValidateTriangulation(pose3D))
            {
                log.WarnFormat("Triangulation validation failed for frame {0} - invalid coordinates", commonFrameNumber);
                return;
            }

            // Transform to golf coordinates
            pose3D = triangulator.TransformToGolfCoords(pose3D);
            if (pose3D == null)
            {
                log.DebugFormat("Coordinate transformation failed for frame {0}", commonFrameNumber);
                return;
            }

            // Validate again after transformation
            if (!triangulator.ValidateTriangulation(pose3D))
            {
                log.DebugFormat("Post-transformation validation failed for frame {0}", commonFrameNumber);
                return;
            }

            // Set frame number to common frame number
            // Use the actual frame number from the poses, not commonFrameNumber
            pose3D.FrameNumber = actualFrameNumber;

            // Store in cache
            pose3DCache.AddOrUpdate(pose3D);
            
            // Save cache periodically (every 10 frames) to avoid losing data
            if (pose3DCache.Poses != null && pose3DCache.Poses.Count % 10 == 0)
            {
                Save3DCache();
            }

            // Notify debug panel of update
            Services.NotificationCenter.RaisePose3DUpdated(this);
            
            log.DebugFormat("Triangulated frame {0}: {1} valid keypoints, cache now has {2} poses", 
                commonFrameNumber, pose3D.ValidKeyPointCount, pose3DCache.Poses != null ? pose3DCache.Poses.Count : 0);

            // Log success occasionally
            var quality = triangulator.GetQualityMetrics(pose3D);
            if (quality != null)
            {
                log.DebugFormat("Triangulated frame {0}: {1} valid keypoints, confidence={2:F2}",
                    commonFrameNumber, quality.ValidKeypointCount, quality.AverageConfidence);
            }
        }

        #endregion
    }
}

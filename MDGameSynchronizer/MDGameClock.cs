using Godot;
using System;

///<summary>
/// The MDGameClock ticks once per _PhysicsProcess(), it will pause when the game is paused.
/// <para>The MDGameClock will automatically adjust it's offset for remote ticks based on connected players ping</para>
///</summary>
[MDAutoRegister]
public class MDGameClock : Node
{
    public static readonly String LOG_CAT = "LogGameClock";

    ///<summary>If we are calculating offset from ping this is the minimum offset we can have</summary>
    public static readonly int MINIMUM_OFFSET = 5;

    protected static readonly float TICK_INTERVAL_MILLISECONDS = 1000 / Engine.IterationsPerSecond;

    protected static readonly float TICK_INTERVAL_DELTA = 1f / (float)Engine.IterationsPerSecond;

    public delegate void GameTickEvent(uint Tick);

    ///<summary>Event triggers every time the clock ticks</summary>
    public event GameTickEvent OnGameTick = delegate {};

    ///<summary>Event triggers if a local tick is skipped (happens if framerate drops for any reason)</summary>
    public event GameTickEvent OnLocalSkippedTick = delegate {};

    ///<summary>Event triggers if a remote tick is skipped to adjust remote tick offset</summary>
    public event GameTickEvent OnRemoteSkippedTick = delegate {};

    ///<summary>Event triggers when ticks are adjusted from the outside, such as when you join a game in progress</summary>
    public event GameTickEvent OnGameTickChanged = delegate {};

    // The current tick
    protected uint CurrentTick = 0;

    // The current remote tick
    protected int LastRemoteTickOffset = 0;
    
    // The current remote tick
    protected int CurrentRemoteTickOffset = 0;

    // Target for the current remote tick
    protected int CurrentRemoteTickOffsetTarget = 0;

    ///<summary>If set it will enforce this offset to remote ticks</summary>
    public int RemoteTickOffset {get; set;} = 0;

    ///<summary>As long as highest ping is within tolerance we will not change offset</summary>
    public float RemoteTickPingTolerance {get; set;} = 0.4f;

    ///<summary>The highest ping will be multiplied by this value before we calculate what the remote tick should be.</summary>
    public float RemoteTickPingModifier {get; set;} = 1f;

    protected bool LastTickDuplicated = false;

    protected float DeltaTickCounter = 0f;

    protected MDGameSynchronizer GameSynchronizer;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Force));
        GameSynchronizer = this.GetGameSynchronizer();
        CurrentRemoteTickOffset = MINIMUM_OFFSET;
        CurrentRemoteTickOffsetTarget = MINIMUM_OFFSET;
        LastRemoteTickOffset = MINIMUM_OFFSET;
        GameSynchronizer.OnPlayerPingUpdatedEvent += OnPlayerPingUpdatedEvent;
    }

    public override void _ExitTree()
    {
        GameSynchronizer.OnPlayerPingUpdatedEvent -= OnPlayerPingUpdatedEvent;
    }

    public override void _Process(float delta)
    {
        if (!GetTree().Paused)
        {
            DeltaTickCounter += delta;
            if (DeltaTickCounter >= TICK_INTERVAL_DELTA)
            {
                // Increase tick counter
                DeltaTickCounter -= TICK_INTERVAL_DELTA;
                CurrentTick++;
                
                // Allow skipping a single tick per update to catch up
                // TODO: Set a limit so ticks are not skipped so often
                if (DeltaTickCounter >= TICK_INTERVAL_DELTA)
                {
                    MDLog.Trace(LOG_CAT, "Tick {0}, was skipped because delta was {1}", CurrentTick, delta);
                    OnLocalSkippedTick(CurrentTick);
                    DeltaTickCounter -= TICK_INTERVAL_DELTA;
                    CurrentTick++;
                }
            }
            AdjustRemoteTickOffset();
            OnGameTick(CurrentTick);
        }
    }

    public void SetCurrentTick(uint Tick)
    {
        MDLog.Debug(LOG_CAT, "Tick changed by code from {0} to {1}", CurrentTick, Tick);
        CurrentTick = Tick;
        OnGameTickChanged(CurrentTick);
    }

    ///<summary>Returns the current local tick</summary>
    public uint GetTick()
    {
        return (uint)Math.Max(CurrentTick, 0);
    }

    ///<summary>Returns the current remote tick used for synchronizing remote players</summary>
    public uint GetRemoteTick()
    {
        return (uint)Math.Max(CurrentTick + CurrentRemoteTickOffset, 0);
    }

    ///<summary>In the event the current tick is a repeated tick this will return true</summary>
    public bool IsRemoteDuplicateTick()
    {
        return false;
    }

    ///<summary>Get time in milliseconds (OS.GetTickMsec() format) until the tick or since the tick</summary>
    public long GetTimeOfTick(uint Tick)
    {
        long differenceBetweenCurrent = Tick - CurrentTick;
        long timeInMilliseconds = (long)Mathf.Floor(differenceBetweenCurrent * TICK_INTERVAL_MILLISECONDS);
        return timeInMilliseconds;
    }

    ///<summary>Adjust the remote tick offset if necessary</summary>
    protected void AdjustRemoteTickOffset()
    {
        LastTickDuplicated = false;
        if (CurrentRemoteTickOffsetTarget != CurrentRemoteTickOffset)
        {
            // Ensure that we don't modify the offset two consecutive frames in a row
            // TODO: Set a limit so ticks are not skipped so often
            if (LastRemoteTickOffset != CurrentRemoteTickOffset)
            {
                LastRemoteTickOffset = CurrentRemoteTickOffset;
                return;
            }

            if (CurrentRemoteTickOffsetTarget > CurrentRemoteTickOffset)
            {
                MDLog.Debug(LOG_CAT, "Increasing remote tick offset from {0} to {1}", CurrentRemoteTickOffset, CurrentRemoteTickOffset+1);
                // Notify that we are skipping this tick
                OnRemoteSkippedTick(GetRemoteTick());
                CurrentRemoteTickOffset++;
            }
            else
            {
                MDLog.Debug(LOG_CAT, "Decreasing remote tick offset from {0} to {1}", CurrentRemoteTickOffset, CurrentRemoteTickOffset-1);
                CurrentRemoteTickOffset--;
                // We need to mark that last tick was a duplicate
                LastTickDuplicated = true;
            }
        }
    }

    protected void OnPlayerPingUpdatedEvent(int PeerId, int Ping)
    {
        CalculateRemoteOffset();
    }

    /// <summary>Attempts to calculate what the remote offset should be based on the current ping</summary>
    protected void CalculateRemoteOffset()
    {
        // Check if we got a fixed offset
        if (RemoteTickOffset > 0)
        {
            MDLog.Trace(LOG_CAT, "Ping offset is set to be static at {0}", RemoteTickOffset);
            CurrentRemoteTickOffsetTarget = RemoteTickOffset;
            return;
        }

        // Check if we got any ping
        int HighestPing = (int)Mathf.Ceil(GameSynchronizer.GetMaxPlayerPing() * RemoteTickPingModifier);
        if (HighestPing == 0)
        {
            CurrentRemoteTickOffsetTarget = MINIMUM_OFFSET;
            MDLog.Trace(LOG_CAT, "We got no ping setting offset to minimum offset of {0}", MINIMUM_OFFSET);
            return; 
        }

        // Calculate offset based on ping
        int newOffset = (int)Mathf.Ceil(HighestPing / TICK_INTERVAL_MILLISECONDS);

        // If it is less than minimum set our target to minimum
        if (newOffset <= MINIMUM_OFFSET)
        {
            CurrentRemoteTickOffsetTarget = MINIMUM_OFFSET;
            MDLog.Trace(LOG_CAT, "Ping offset of {0} is less than our minimum offset of {1}", newOffset, MINIMUM_OFFSET);
            return;
        }

        // Calculate the difference between new and old
        float difference = Mathf.Abs((newOffset - CurrentRemoteTickOffset) / newOffset);

        if (difference >= RemoteTickPingTolerance)
        {
            MDLog.Trace(LOG_CAT, "Ping difference is too large adjust remote tick offset target from {0} to {1}", CurrentRemoteTickOffsetTarget, newOffset);
            // We need to adjust the remote tick offset
            CurrentRemoteTickOffsetTarget = newOffset;
        }
    }
}

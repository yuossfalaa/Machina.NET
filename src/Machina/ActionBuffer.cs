﻿using System;
using System.Collections.Generic;

namespace Machina;

/// <summary>
/// A class that manages a FIFO list of Actions.
/// </summary>
/// <remarks>
/// Main constructor.
/// </remarks>
internal class ActionBuffer(RobotCursor parent)
{
    /// <summary>
    /// Actions pending to be released.
    /// </summary>
    private List<Action> pending = new List<Action>();

    /// <summary>
    /// Keep track of past released actions.
    /// </summary>
    private List<Action> released = new List<Action>();

    /// <summary>
    /// Stores the amount of Actions per 'block'. 
    /// A 'block' is a set of Actions flagged to be released as a group,
    /// like in Execute mode. 
    /// </summary>
    private List<int> blockCounts = new List<int>();

    private RobotCursor _parent = parent;

    /// <summary>
    /// Add an Action to the pending buffer.
    /// </summary>
    /// <param name="act"></param>
    /// <returns></returns>
    public bool Add(Action act)
    {
        pending.Add(act);
        return true;
    }

    /// <summary>
    /// Returns the id of the next Action pending release, without releasing it. 
    /// </summary>
    /// <returns></returns>
    public int QueryIdOfNext() => pending.Count > 0 ? pending[0].Id : -1;


    /// <summary>
    /// Release the next pending Action with the highest priority.
    /// </summary>
    /// <returns></returns>
    public Action GetNext()
    {
        if (pending.Count == 0) return null;

        Action next = pending[0];

        released.Add(next);
        pending.RemoveAt(0);

        // update blockcounts
        if (blockCounts.Count > 0)
        {
            if (--blockCounts[0] <= 0)
            {
                blockCounts.RemoveAt(0);
            }
        }

        return next;
    }

    /// <summary>
    /// Returns the last Action that was released by the buffer
    /// </summary>
    /// <returns></returns>
    public Action GetLast()
    {
        if (released.Count == 0) return null;
        return released[released.Count - 1];
    }

    /// <summary>
    /// Release all pending Actions in the order they were issued.
    /// </summary>
    /// <param name="flush">If true, pending actions will be flushed from the buffer and flagged as released</param>
    /// <returns></returns>
    public List<Action> GetAllPending(bool flush)
    {
        List<Action> proc = new List<Action>();
        foreach (Action a in pending) proc.Add(a);  // shallow copy
        if (flush)
        {
            released.AddRange(pending);
            pending.Clear();
        }
        return proc;
    }

    /// <summary>
    /// Release all pending Actions in the order they were issued.
    /// </summary>
    /// <returns></returns>
    public List<Action> GetAllPending()
    {
        return GetAllPending(true);
    }

    /// <summary>
    /// Returns all Actions in the pending buffer until the one with given id inclusive.
    /// This assumes ids are correlative and ascending, will stop if it finds an
    /// id larger than the given one. 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public List<Action> GetAllUpToId(int id)
    {
        List<Action> search = new List<Action>();
        int count = 0;
        foreach (Action a in pending)
        {
            if (a.Id <= id) count++;
            else break;
        }

        if (count == 0)
        {
            released.AddRange(search);
            return search;
        }

        // If the action wasn't found (is this even possible??)
        if (count == pending.Count && pending[pending.Count - 1].Id != id)
        {
            throw new ArgumentException($"Couldn't find id {id} in the the action buffer");
        }

        for (int i = 0; i < count; i++)
        {
            search.Add(GetNext());
        }

        released.AddRange(search);
        return search;
    }

    /// <summary>
    /// Wraps all pending actions outside release blocks into one.
    /// </summary>
    public void SetBlock()
    {
        int sum = 0;
        foreach (var i in blockCounts) sum += i;
        blockCounts.Add(pending.Count - sum);
    }

    /// <summary>
    /// Returns the next block of Actions to be released. If no block
    /// is present, it will return all pending Actions. 
    /// </summary>
    /// <param name="flush">If true, this block will be moved 
    /// from pending to released.</param>
    /// <returns></returns>
    public List<Action> GetBlockPending(bool flush)
    {
        List<Action> acts;
        if (blockCounts.Count > 0 && blockCounts[0] > 0)
        {
            acts = pending.GetRange(0, blockCounts[0]);
            if (flush)
            {
                pending.RemoveRange(0, blockCounts[0]);
                released.AddRange(acts);
                blockCounts.RemoveAt(0);
            }
        }
        else
        {
            acts = GetAllPending(flush);
        }
        return acts;
    }

    /// <summary>
    /// Is there any Action pending in the buffer?
    /// </summary>
    /// <returns></returns>
    public bool AreActionsPending()
    {
        return pending.Count > 0;
    }

    /// <summary>
    /// How many Actions are pending in the buffer?
    /// </summary>
    /// <returns></returns>
    public int ActionsPendingCount()
    {
        return pending.Count;
    }

    /// <summary>
    /// Has any Action ever been issued to this buffer?
    /// </summary>
    /// <returns></returns>
    public bool IsVirgin()
    {
        return released.Count == 0 && pending.Count == 0;
    }

    /// <summary>
    /// Clear all buffered and past released Actions.
    /// </summary>
    public void Flush()
    {
        released.Clear();
        pending.Clear();
    }

    /// <summary>
    /// Writes a description of each pending Action to the Console.
    /// </summary>
    public void DebugBufferedActions()
    {
        Logger.Debug("--> RELEASED:");
        foreach (Action a in released) Logger.Debug("    " + a);

        Logger.Debug("--> PENDING: ");
        string blocks = "";
        foreach (var i in blockCounts) blocks += i + ",";
        Logger.Debug(blocks);
        Logger.Debug("");
        int it = -1;
        int b = 0;
        foreach (Action a in pending)
        {
            if (blockCounts.Count > 0 && b < blockCounts.Count)
            {
                it++;
                if (it >= blockCounts[b])
                {
                    b++;
                    if (b >= blockCounts.Count)
                    {
                        it = -1;
                    }
                    else
                    {
                        it = 0;
                    }
                }
            }

            if (it == 0)
            {
                Logger.Debug("    Block " + b + ":");
            }

            if (it >= 0)
            {
                Logger.Debug("        " + a);
            }
            else
            {
                Logger.Debug("    " + a);
            }
        }
    }

    public override string ToString()
    {
        return string.Format("ACTION BUFFER FROM {2}: {0} issued, {1} remaining", released.Count, pending.Count, this._parent.name);
    }
}

﻿/*
    Copyright 2015 MCGalaxy
        
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.Collections.Generic;
using System.Threading;

namespace MCGalaxy {
    public sealed partial class Scheduler {

        readonly List<SchedulerTask> tasks = new List<SchedulerTask>();
        readonly AutoResetEvent handle = new AutoResetEvent(false);
        readonly Thread thread;
        readonly object taskLock = new object();

        public Scheduler(string name) {
            thread = new Thread(Loop);
            thread.Name = name;
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary> Queues an action that is asynchronously executed one time, as soon as possible. </summary>
        public SchedulerTask QueueOnce(Action callback)
        {
            return EnqueueTask(new SchedulerTask(obj => callback(), null, TimeSpan.Zero, false));
        }

        /// <summary> Queues an action that is asynchronously executed one time, after a certain delay. </summary>
        public SchedulerTask QueueOnce(Action<SchedulerTask> callback, object state, TimeSpan delay) {
            return EnqueueTask(new SchedulerTask(callback, state, delay, false));
        }
        
        /// <summary> Queues an action that is asynchronously executed repeatedly, after a certain delay. </summary>
        public SchedulerTask QueueRepeat(Action<SchedulerTask> callback, object state, TimeSpan delay) {
            return EnqueueTask(new SchedulerTask(callback, state, delay, true));
        }
        
        /// <summary> Cancels a task if it is in the tasks list.
        /// <remarks> Does not cancel the task if it is currently executing. </remarks>
        public bool Cancel(SchedulerTask task) {
            lock (taskLock) {
                return tasks.Remove(task);
            }
        }
        
        
        SchedulerTask EnqueueTask(SchedulerTask task) {
            lock (taskLock) {
                tasks.Add(task);
                handle.Set();
            }
            return task;
        }

        void Loop() {
            while (true) {
                SchedulerTask task = GetNextTask();
                if (task != null) DoTask(task);
                handle.WaitOne(GetWaitTime(), false);
            }
        }
        
        
        SchedulerTask GetNextTask() {
            DateTime now = DateTime.UtcNow;
            lock (taskLock) {
                foreach (SchedulerTask task in tasks) {
                    if (task.NextRun < now) return task;
                }
            }
            return null;
        }
        
        void DoTask(SchedulerTask task) {
            try {
                task.Callback(task);
            } catch (Exception ex) {
                MCGalaxy.Server.ErrorLog(ex);
            }
            
            if (task.Repeating) {
                task.NextRun = DateTime.UtcNow.Add(task.Delay);
            } else {
                lock (taskLock)
                    tasks.Remove(task);
            }
        }
        
        int GetWaitTime() {
            int wait = int.MaxValue;
            DateTime now = DateTime.UtcNow;
            
            lock (taskLock) {
                foreach (SchedulerTask task in tasks) {
                    int remaining = (int)(task.NextRun - now).TotalMilliseconds;
                    // minimum wait time is 10 milliseconds
                    remaining = Math.Max(10, remaining);
                    wait = Math.Min(wait, remaining);
                }
            }
            return wait == int.MaxValue ? -1 : wait;
        }
    }

    public class SchedulerTask {
        public Action<SchedulerTask> Callback;
        public object State;
        
        /// <summary> Interval between executions of this task. </summary>
        public TimeSpan Delay;
        
        /// <summary> Point in time this task should next be executed. </summary>
        public DateTime NextRun;
        
        /// <summary> Whether this task should continue repeating. </summary>
        public bool Repeating;
        
        public SchedulerTask(Action<SchedulerTask> callback, object state,
                             TimeSpan delay, bool repeating) {
            Callback = callback;
            State = state;
            Delay = delay;
            NextRun = DateTime.UtcNow.Add(delay);
            Repeating = repeating;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SaveManagement
{
    /// <summary>
    /// Writes an already prepared save snapshot on a background thread.
    /// </summary>
    /// <remarks>
    /// Compressing a large .opc takes seconds: on a 42 MB save the gzip step
    /// alone is about 4.5 s. Doing that on the main thread froze the whole game
    /// for the duration of every write, which the player saw as the game
    /// hanging. Here the game only builds the snapshot (fast) and the queue
    /// compresses and commits it; a single worker keeps writes in order, so a
    /// later save can never be replaced by an earlier one.
    /// The worker never touches the UI: every finished write is queued and
    /// delivered by <see cref="Pump"/> on the main thread, so the caller learns
    /// the real result and a failure becomes visible in the game.
    /// </remarks>
    public static class SaveWriter
    {
        sealed class Job
        {
            public string path, json, content;
            public Action<bool, string> done;
        }

        sealed class Result
        {
            public bool ok;
            public string error;
            public Action<bool, string> done;
        }

        /// <summary>
        /// A big save takes about 7 s here; this still covers a slow disk, but a
        /// quit can no longer look frozen for two minutes when a write is stuck.
        /// </summary>
        public const int DefaultFlushTimeoutMs = 20000;

        static readonly object gate = new object();
        static readonly Queue<Job> queue = new Queue<Job>();
        static readonly Queue<Result> results = new Queue<Result>();
        static Thread worker;
        static bool busy;
        static long completed, failed;

        public static long Completed { get { lock (gate) return completed; } }
        public static long Failed { get { lock (gate) return failed; } }
        public static bool HasPending { get { lock (gate) return busy || queue.Count > 0; } }

        /// <summary>Queue one snapshot: UTF-8 JSON of GameData plus the scene content.</summary>
        /// <param name="done">
        /// Called on the main thread with the real outcome, or not at all when the
        /// game is gone before the write finishes. Never assume success from the
        /// mere fact that the snapshot was queued.
        /// </param>
        public static void Enqueue(string path, string json, string content,
            Action<bool, string> done = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[Save] cannot write a save without a path");
                Deliver(false, "save path is empty", done);
                return;
            }

            var job = new Job
            {
                path = path,
                json = json ?? string.Empty,
                content = content ?? string.Empty,
                done = done
            };
            lock (gate)
            {
                queue.Enqueue(job);
                if (worker == null || !worker.IsAlive)
                {
                    worker = new Thread(Run);
                    worker.IsBackground = true;
                    worker.Name = "SaveWriter";
                    worker.Start();
                }
            }
        }

        static void Run()
        {
            for (;;)
            {
                Job job;
                lock (gate)
                {
                    if (queue.Count == 0) { worker = null; return; }
                    job = queue.Dequeue();
                    busy = true;
                }

                bool ok = true;
                string error = null;
                try
                {
                    SaveStorage.WriteAtomic(job.path, SaveUtility.Encode(job.json + "\n" + job.content));
                    SaveCatalog.Invalidate(job.path);
                }
                catch (Exception e)
                {
                    ok = false;
                    error = e.GetBaseException().Message;
                    Debug.LogError("[Save] write failed for " + job.path + ": " + e);
                }
                finally
                {
                    lock (gate)
                    {
                        if (ok) completed++; else failed++;
                        results.Enqueue(new Result { ok = ok, error = error, done = job.done });
                        busy = false;
                    }
                }
            }
        }

        /// <summary>
        /// Hand finished writes to the game. Must run on the main thread (once a
        /// frame is enough); it never blocks and does nothing when idle.
        /// </summary>
        public static void Pump()
        {
            for (;;)
            {
                Result result;
                lock (gate)
                {
                    if (results.Count == 0) return;
                    result = results.Dequeue();
                }
                Deliver(result.ok, result.error, result.done);
                // A write that did not reach the disk is a real problem: the
                // world keeps running with an older file, so say it on screen.
                // A notice problem must never swallow the caller's result.
                if (result.ok) continue;
                try { SaveFailureNotice.Show(null); }
                catch (Exception e) { Debug.LogWarning("[Save] could not show the failure notice: " + e); }
            }
        }

        static void Deliver(bool ok, string error, Action<bool, string> done)
        {
            if (done != null)
            {
                try { done(ok, error); }
                catch (Exception e) { Debug.LogError("[Save] write result callback failed: " + e); }
            }
        }

        /// <summary>
        /// Block until every queued snapshot is on disk. Used when another save
        /// is loaded, on quit and when the scene is torn down: nothing may
        /// outlive the game session unwritten. Returns false when a write was
        /// still running at the timeout (its failure is reported on screen).
        /// </summary>
        public static bool Flush(int timeoutMs = DefaultFlushTimeoutMs)
        {
            Pump();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (HasPending)
            {
                if (clock.ElapsedMilliseconds > timeoutMs)
                {
                    Debug.LogWarning("[Save] write is still running after " + timeoutMs + " ms");
                    SaveFailureNotice.Show(null);
                    return false;
                }
                Thread.Sleep(15);
                Pump();
            }
            Pump();
            return true;
        }
    }
}

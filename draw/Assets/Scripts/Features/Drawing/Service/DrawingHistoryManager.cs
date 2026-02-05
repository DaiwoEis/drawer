using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.Interface;
using Features.Drawing.App.Command;
using Features.Drawing.App.Interface;

namespace Features.Drawing.Service
{
    /// <summary>
    /// Manages the command history, undo/redo stacks, and synchronization state.
    /// Extracted from DrawingAppService to separate concerns.
    /// </summary>
    public class DrawingHistoryManager
    {
        // State
        private List<ICommand> _history = new List<ICommand>();
        private List<ICommand> _redoHistory = new List<ICommand>();
        private List<ICommand> _archivedHistory = new List<ICommand>();
        
        // Tracks IDs of currently active strokes (history + archived)
        private HashSet<string> _activeStrokeIds = new HashSet<string>();

        public DrawingHistoryManager()
        {
        }

        // Public Accessors
        public IReadOnlyList<ICommand> History => _history;
        public IReadOnlyList<ICommand> ArchivedHistory => _archivedHistory;
        public HashSet<string> ActiveStrokeIds => _activeStrokeIds;
        public bool CanUndo => _history.Count > 0;
        public bool CanRedo => _redoHistory.Count > 0;

        // Hooks for subclasses (Visual Layer)
        protected virtual void OnCommandAdded(ICommand cmd) { }
        protected virtual void OnCommandArchiving(ICommand cmd) { }
        protected virtual void OnHistoryChanged() { }
        protected virtual void ExecuteCommandVisual(ICommand cmd) { }

        /// <summary>
        /// Adds a command to history and handles sliding window/baking.
        /// </summary>
        public virtual void AddCommand(ICommand cmd)
        {
            Debug.Log($"[History] Added command: {cmd.GetType().Name} [ID: {cmd.Id}]. Count: {_history.Count + 1}");
            _history.Add(cmd);
            _activeStrokeIds.Add(cmd.Id);

            _redoHistory.Clear();

            // Hook: Visual update
            OnCommandAdded(cmd);

            // Maintain sliding window (Keep last 50 active)
            while (_history.Count > 50)
            {
                var removedCmd = _history[0];
                
                // Hook: Baking before removal
                OnCommandArchiving(removedCmd);

                // Archive it (Logical Save)
                _archivedHistory.Add(removedCmd);
                // Note: We KEEP the ID in _activeStrokeIds because it is still part of the drawing
                
                // Optimization: If the baked command is a ClearCanvas, 
                // we can safely discard all previous archive history to save RAM.
                if (removedCmd is ClearCanvasCommand)
                {
                    // Everything before a Clear is visually irrelevant.
                    foreach (var archivedCmd in _archivedHistory)
                    {
                        if (archivedCmd != removedCmd)
                        {
                            _activeStrokeIds.Remove(archivedCmd.Id);
                        }
                    }

                    _archivedHistory.Clear();
                    _archivedHistory.Add(removedCmd);
                }
                
                _history.RemoveAt(0);
            }
        }

        public virtual void Undo()
        {
            if (_history.Count == 0) return;

            // Remove last command
            var cmd = _history[_history.Count - 1];
            Debug.Log($"[Undo] Reverting command [ID: {cmd.Id}]");
            _history.RemoveAt(_history.Count - 1);
            
            _activeStrokeIds.Remove(cmd.Id);

            // Add to Redo history
            _redoHistory.Add(cmd);
            
            OnHistoryChanged();
        }

        public virtual void Redo()
        {
            if (_redoHistory.Count == 0) return;

            // Remove last redo item
            var cmd = _redoHistory[_redoHistory.Count - 1];
            Debug.Log($"[Redo] Restoring command [ID: {cmd.Id}]");
            _redoHistory.RemoveAt(_redoHistory.Count - 1);
            
            // Add back to history
            _history.Add(cmd);
            _activeStrokeIds.Add(cmd.Id);

            OnHistoryChanged();
        }

        /// <summary>
        /// Gets the complete history (Archived + Active) for synchronization.
        /// </summary>
        public List<ICommand> GetFullHistory()
        {
            var fullList = new List<ICommand>(_archivedHistory.Count + _history.Count);
            fullList.AddRange(_archivedHistory);
            fullList.AddRange(_history);
            return fullList;
        }

        /// <summary>
        /// Gets incremental history updates (commands added after the given lastSequenceId).
        /// Returns all commands if lastSequenceId is not found or history is diverged.
        /// </summary>
        public List<ICommand> GetIncrementalHistory(long lastSequenceId)
        {
            var incrementalList = new List<ICommand>();
            
            // Check Active History first (most likely)
            int index = _history.FindIndex(c => c.SequenceId == lastSequenceId);
            if (index != -1)
            {
                // Found in active history, return everything after it
                if (index + 1 < _history.Count)
                {
                    incrementalList.AddRange(_history.GetRange(index + 1, _history.Count - (index + 1)));
                }
                return incrementalList;
            }

            // Check Archived History
            index = _archivedHistory.FindIndex(c => c.SequenceId == lastSequenceId);
            if (index != -1)
            {
                // Found in archive
                // Add remaining archive
                if (index + 1 < _archivedHistory.Count)
                {
                    incrementalList.AddRange(_archivedHistory.GetRange(index + 1, _archivedHistory.Count - (index + 1)));
                }
                // Add all active
                incrementalList.AddRange(_history);
                return incrementalList;
            }

            // Fallback: Full Sync if ID not found (client is too far behind or diverged)
            return GetFullHistory();
        }

        /// <summary>
        /// Replaces the current local history with a remote authoritative history.
        /// </summary>
        public virtual void ReplaceHistory(List<ICommand> remoteHistory)
        {
            // 1. Clear everything
            _history.Clear();
            _redoHistory.Clear();
            _archivedHistory.Clear();
            // Collision service clear must be handled by caller or subclass
            _activeStrokeIds.Clear();
            
            // Subclass hook for clearing visual state is handled by overriding this method or hook?
            // Since we cleared lists, subclass override should handle visual clear.

            // 2. Replay all commands (Execute without adding to history)
            foreach (var cmd in remoteHistory)
            {
                ExecuteCommandVisual(cmd);
                _activeStrokeIds.Add(cmd.Id);
            }

            // 3. Rebuild internal lists
            int total = remoteHistory.Count;
            int activeCount = Mathf.Min(total, 50);
            int archiveCount = total - activeCount;

            if (archiveCount > 0)
            {
                _archivedHistory.AddRange(remoteHistory.GetRange(0, archiveCount));
            }
            
            if (activeCount > 0)
            {
                _history.AddRange(remoteHistory.GetRange(archiveCount, activeCount));
            }
        }

        /// <summary>
        /// Generates a lightweight checksum (hash) of the current history state.
        /// </summary>
        public string GetHistoryChecksum()
        {
            long hash = 0;
            foreach (var cmd in _archivedHistory)
            {
                hash = (hash * 31) + cmd.Id.GetHashCode();
            }
            foreach (var cmd in _history)
            {
                hash = (hash * 31) + cmd.Id.GetHashCode();
            }
            return hash.ToString("X");
        }

        /// <summary>
        /// Rebuilds the BakedRT from the logical archive.
        /// </summary>
        public virtual void RebuildBackBuffer()
        {
            // Implementation moved to VisualDrawingHistoryManager
        }
    }
}

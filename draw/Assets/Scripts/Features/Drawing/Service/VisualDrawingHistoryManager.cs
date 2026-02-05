using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.Interface;
using Features.Drawing.App.Command;
using Features.Drawing.App.Interface;

namespace Features.Drawing.Service
{
    /// <summary>
    /// Extension of DrawingHistoryManager that handles visual representation.
    /// Manages rendering, baking, and smoothing services.
    /// </summary>
    public class VisualDrawingHistoryManager : DrawingHistoryManager
    {
        private readonly IStrokeRenderer _renderer;

        public VisualDrawingHistoryManager(IStrokeRenderer renderer) : base()
        {
            _renderer = renderer;
        }

        public override void AddCommand(ICommand cmd)
        {
            // 1. Add to data history
            base.AddCommand(cmd);

            // 2. Visual: Execute immediately (Draw)
            // Note: base.AddCommand handles the logic addition. 
            // We need to verify if base.AddCommand triggers any side effects we need to replicate or hook into.
            // base.AddCommand calls logic for sliding window. We need to hook into the sliding window for baking.
            
            // Actually, we should override the baking logic which was inside AddCommand.
            // Since we can't easily inject code into the middle of base.AddCommand,
            // we might need to rely on virtual methods called by base, or duplicate some logic if the base is not designed for extension points.
            // But for now, let's assume we refactor base to call virtual OnCommandAdded or similar.
            
            // However, the previous implementation of AddCommand did:
            // 1. Add to list
            // 2. Maintain sliding window (Remove -> Bake -> Archive)
            
            // We will need to refactor the base class significantly to allow this separation.
            // For this initial implementation file, I will assume the base class 
            // will be refactored to expose virtual methods for "OnCommandRemoving" or similar.
            
            // Wait, to keep it simple and robust:
            // I will implement the visual update *after* the base data update, 
            // AND I need to handle the baking of removed items.
            // The cleanest way is if the Base class exposes a virtual "OnArchiveCommand(ICommand cmd)" method.
        }

        protected override void OnCommandAdded(ICommand cmd)
        {
            // Do NOT execute visual command here.
            // Commands added to history are assumed to be already executed (Local input or Remote commit).
            // Re-executing here would cause double-draw (transparency artifacts) or redundant work.
        }

        protected override void OnCommandArchiving(ICommand cmd)
        {
            // Bake the command into the back buffer before it is removed from active history
            if (_renderer != null)
            {
                _renderer.SetBakingMode(true);
                cmd.Execute(_renderer);
                _renderer.SetBakingMode(false);
            }
        }

        protected override void OnHistoryChanged()
        {
            RedrawHistory();
        }

        public override void RebuildBackBuffer()
        {
            if (_renderer == null) return;
            
            _renderer.SetBakingMode(true);
            _renderer.ClearCanvas();
            
            foreach (var cmd in ArchivedHistory)
            {
                cmd.Execute(_renderer);
            }
            
            _renderer.SetBakingMode(false);
            
            RedrawHistory();
            
            Debug.Log($"[VisualHistory] Rebuilt BackBuffer from {ArchivedHistory.Count} archived commands.");
        }

        public override void ReplaceHistory(List<ICommand> remoteHistory)
        {
             // Clear visual state first
            _renderer.ClearCanvas();
            
            // Update data (base will clear lists)
            base.ReplaceHistory(remoteHistory);
            
            // Re-execute all is handled by base calling OnCommandAdded? 
            // No, base.ReplaceHistory just updates lists usually.
            // Let's check the base implementation plan.
            // If base.ReplaceHistory just modifies lists, we need to manually redraw or re-execute.
            
            // In the original code, ReplaceHistory executed commands directly.
            // We should override this to handle visual execution.
        }
        
        // Helper for ReplaceHistory override
        protected override void ExecuteCommandVisual(ICommand cmd)
        {
            cmd.Execute(_renderer);
        }

        private void RedrawHistory()
        {
            if (_renderer == null) return;

            // 1. Determine start state
            int startIndex = 0;
            bool fullClear = false;

            var history = History; // Access base property

            // Check if we have a ClearCanvasCommand in history
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i] is ClearCanvasCommand)
                {
                    startIndex = i;
                    fullClear = true;
                    break;
                }
            }

            // 2. Prepare Canvas
            if (fullClear)
            {
                _renderer.ClearCanvas();
            }
            else
            {
                _renderer.RestoreFromBackBuffer();
            }
            
            // 3. Replay commands
            for (int i = startIndex; i < history.Count; i++)
            {
                history[i].Execute(_renderer);
            }
        }
    }
}

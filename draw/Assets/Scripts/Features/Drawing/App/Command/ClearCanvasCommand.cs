using Features.Drawing.Domain.Command;
using Features.Drawing.Service;
using Features.Drawing.Domain.Interface;

namespace Features.Drawing.App.Command
{
    public class ClearCanvasCommand : ClearCanvasData, ICommand
    {
        public ClearCanvasCommand(long sequenceId) : base(System.Guid.NewGuid().ToString(), sequenceId)
        {
        }

        public void Execute(IStrokeRenderer renderer, StrokeSmoothingService smoothingService)
        {
            renderer.ClearCanvas();
        }
    }
}

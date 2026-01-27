using Features.Drawing.Domain.Interface;
using Features.Drawing.Service;

namespace Features.Drawing.App.Command
{
    public class ClearCanvasCommand : ICommand
    {
        public string Id { get; } = System.Guid.NewGuid().ToString();

        public void Execute(IStrokeRenderer renderer, StrokeSmoothingService smoothingService)
        {
            renderer.ClearCanvas();
        }
    }
}

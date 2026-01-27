using Features.Drawing.Domain.Interface;

namespace Features.Drawing.App.Command
{
    public interface ICommand
    {
        string Id { get; }
        void Execute(IStrokeRenderer renderer, Features.Drawing.Service.StrokeSmoothingService smoothingService);
    }
}

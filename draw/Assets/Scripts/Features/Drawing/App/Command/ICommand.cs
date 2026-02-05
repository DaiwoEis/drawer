using Features.Drawing.Domain.Interface;

namespace Features.Drawing.App.Command
{
    public interface ICommand : ICommandData
    {
        void Execute(IStrokeRenderer renderer);
    }
}

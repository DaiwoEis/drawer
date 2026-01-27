using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.App.Interface
{
    public interface IInputHandler
    {
        void StartStroke(LogicPoint point);
        void MoveStroke(LogicPoint point);
        void EndStroke();
    }
}

using Features.Drawing.Domain.Interface;

namespace Features.Drawing.Domain.Command
{
    [System.Serializable]
    public class ClearCanvasData : ICommandData
    {
        public string Id { get; protected set; }
        public long SequenceId { get; protected set; }

        public ClearCanvasData(string id, long sequenceId)
        {
            Id = id;
            SequenceId = sequenceId;
        }

        protected ClearCanvasData() { }
    }
}
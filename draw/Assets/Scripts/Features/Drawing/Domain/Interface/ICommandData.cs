namespace Features.Drawing.Domain.Interface
{
    /// <summary>
    /// Pure data interface for commands.
    /// Safe for serialization and non-Unity environments.
    /// </summary>
    public interface ICommandData
    {
        string Id { get; }
        long SequenceId { get; }
    }
}
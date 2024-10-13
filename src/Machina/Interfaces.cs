namespace Machina;

/// <summary>
/// Makes an object serializable into the Machina API instruction that would generate an equal instance of it.
/// </summary>
interface IInstructable
{
    string ToInstruction();
}

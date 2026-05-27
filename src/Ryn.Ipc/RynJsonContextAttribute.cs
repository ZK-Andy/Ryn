namespace Ryn.Ipc;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RynJsonContextAttribute(Type contextType) : Attribute
{
    public Type ContextType { get; } = contextType;
}

namespace Ryn.Ipc;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RynCommandAttribute : Attribute
{
    public RynCommandAttribute() { }

    public RynCommandAttribute(string name)
    {
        Name = name;
    }

    public string? Name { get; }
}

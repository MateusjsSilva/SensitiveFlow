namespace LGPD.NET.Core.Exceptions;

public sealed class DataNotFoundException : Exception
{
    public string Entity { get; }
    public string Id { get; }

    public DataNotFoundException(string entity, string id)
        : base($"Data not found for entity '{entity}' with id '{id}'.")
    {
        Entity = entity;
        Id = id;
    }
}

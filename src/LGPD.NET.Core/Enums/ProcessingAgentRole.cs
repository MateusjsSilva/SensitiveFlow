namespace LGPD.NET.Core.Enums;

/// <summary>
/// Roles of processing agents and governance actors under the LGPD.
/// </summary>
public enum ProcessingAgentRole
{
    /// <summary>Controller, responsible for decisions regarding processing.</summary>
    Controller = 0,

    /// <summary>Processor, which processes data on behalf of a controller.</summary>
    Processor,

    /// <summary>Joint controller sharing processing decisions.</summary>
    JointController,

    /// <summary>Sub-processor engaged by a processor.</summary>
    SubProcessor,

    /// <summary>Data protection officer or person in charge.</summary>
    Dpo
}

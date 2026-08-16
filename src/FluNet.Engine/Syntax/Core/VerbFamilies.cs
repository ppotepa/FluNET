namespace FluNET.Syntax.Core;

/// <summary>
/// Semantic verb-family markers. Concrete verbs may use these directly or inherit
/// from legacy abstract verb bases. The language compiler treats both forms as metadata.
/// </summary>
public interface IGet : IVerb { }
public interface ISave : IVerb { }
public interface ILoad : IVerb { }
public interface ISend : IVerb { }
public interface IDelete : IVerb { }
public interface IDownload : IVerb { }
public interface IPost : IVerb { }
public interface ITransform : IVerb { }
public interface ISay : IVerb { }

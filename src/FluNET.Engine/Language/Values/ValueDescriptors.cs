namespace FluNET.Language.Values;

public sealed record ValueCodecDescriptor(TypeId TypeId, Type RuntimeType, Type CodecType);

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// Represents the header of an Open Bitstream Unit (OBU), as defined in the AV1 specification,
/// section 5.3.2 (<c>obu_header</c>) and 5.3.3 (<c>obu_extension_header</c>).
/// </summary>
internal readonly struct ObuHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObuHeader"/> struct.
    /// </summary>
    /// <param name="type">The OBU type.</param>
    /// <param name="hasExtension">Whether the extension header is present.</param>
    /// <param name="hasSize">Whether the OBU carries an explicit size field.</param>
    /// <param name="temporalId">The temporal layer id from the extension header.</param>
    /// <param name="spatialId">The spatial layer id from the extension header.</param>
    public ObuHeader(ObuType type, bool hasExtension, bool hasSize, int temporalId, int spatialId)
    {
        this.Type = type;
        this.HasExtension = hasExtension;
        this.HasSize = hasSize;
        this.TemporalId = temporalId;
        this.SpatialId = spatialId;
    }

    /// <summary>
    /// Gets the OBU type.
    /// </summary>
    public ObuType Type { get; }

    /// <summary>
    /// Gets a value indicating whether the extension header is present.
    /// </summary>
    public bool HasExtension { get; }

    /// <summary>
    /// Gets a value indicating whether the OBU carries an explicit <c>obu_size</c> field.
    /// </summary>
    public bool HasSize { get; }

    /// <summary>
    /// Gets the temporal layer id, or 0 when no extension header is present.
    /// </summary>
    public int TemporalId { get; }

    /// <summary>
    /// Gets the spatial layer id, or 0 when no extension header is present.
    /// </summary>
    public int SpatialId { get; }
}

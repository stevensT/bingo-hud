namespace BingoHud.Core;

/// <summary>
/// The products whose usage windows Bingo shows (codex-quota AC-7a).
///
/// <para>
/// Claude is first on purpose. Alert state written by 0.4.0 carries no product, and reads back
/// as the enum's default, which is the only product 0.4.0 knew.
/// </para>
/// </summary>
public enum Product
{
    Claude,
    Codex,
}

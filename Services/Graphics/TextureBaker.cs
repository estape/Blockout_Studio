using System.Text;

namespace UE5BlockoutStudio.Services.Graphics;

public static class TextureBaker
{
    /// <summary>
    /// Generates a crisp, vectorized UE5 modular grid texture as an SVG Data URL.
    /// Features:
    /// - 100 cm major grid line (orange accent)
    /// - 50 cm sub-grid subdivisions (subtle grey)
    /// - Diagonal prototype hatching for CAD / SketchUp feel
    /// </summary>
    public static string GetUe5GridDataUrl()
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='256' height='256' viewBox='0 0 256 256'>");
        // Base fill
        sb.Append("<rect width='256' height='256' fill='#586273'/>");
        // Alternating quadrant tiles
        sb.Append("<rect x='0' y='0' width='128' height='128' fill='#4f5867'/>");
        sb.Append("<rect x='128' y='128' width='128' height='128' fill='#4f5867'/>");
        // 50cm subgrid line
        sb.Append("<line x1='0' y1='128' x2='256' y2='128' stroke='#697588' stroke-width='2'/>");
        sb.Append("<line x1='128' y1='0' x2='128' y2='256' stroke='#697588' stroke-width='2'/>");
        // Outer 100cm border (major grid)
        sb.Append("<rect x='1' y='1' width='254' height='254' fill='none' stroke='#8b98ad' stroke-width='3'/>");
        // Center crosshair marker
        sb.Append("<line x1='122' y1='128' x2='134' y2='128' stroke='#f58220' stroke-width='2'/>");
        sb.Append("<line x1='128' y1='122' x2='128' y2='134' stroke='#f58220' stroke-width='2'/>");
        sb.Append("</svg>");

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        return $"data:image/svg+xml;base64,{base64}";
    }
}


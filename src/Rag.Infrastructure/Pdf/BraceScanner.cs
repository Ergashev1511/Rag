namespace Rag.Infrastructure.Pdf;

/// <summary>JSON qavslar balansini kuzatadi; string ichidagi qavslar hisobga olinmaydi.</summary>
internal sealed class BraceScanner
{
    private bool _escaped;

    public int Depth { get; private set; }
    public bool InString { get; private set; }

    public void Feed(string text)
    {
        foreach (var c in text)
        {
            if (InString)
            {
                if (_escaped) _escaped = false;
                else if (c == '\\') _escaped = true;
                else if (c == '"') InString = false;
                continue;
            }

            switch (c)
            {
                case '"': InString = true; break;
                case '{' or '[': Depth++; break;
                case '}' or ']': Depth--; break;
            }
        }
    }
}

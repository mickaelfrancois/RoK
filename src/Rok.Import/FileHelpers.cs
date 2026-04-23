namespace Rok.Import;

static internal class FileHelpers
{
    private const int FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00400000;

    public static bool IsOnline(string file)
    {
        FileAttributes attributes = File.GetAttributes(file);

        bool isOnline = (((int)attributes) & FILE_ATTRIBUTE_RECALL_ON_OPEN) != 0;

        return isOnline;
    }
}

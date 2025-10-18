using UnityEditor;

public class CsprojPostprocessor : AssetPostprocessor
{
    public static string OnGeneratedCSProject(string path, string content)
    {
        if (path.EndsWith("Assembly-CSharp.csproj"))
        {
            // Insert the reference to System.Windows.Forms
            if (!content.Contains("System.Windows.Forms"))
            {
                int insertIndex = content.IndexOf("</ItemGroup>");
                if (insertIndex > 0)
                {
                    string reference = "    <Reference Include=\"System.Windows.Forms\" />\n";
                    content = content.Insert(insertIndex, reference);
                }
            }
        }
        return content;
    }
}

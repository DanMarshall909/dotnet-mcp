using DotNetMcp.Core.Models;

namespace DotNetMcp.Core.Services;

public static class DeltaGenerator
{
    public static RefactoringDelta GenerateDelta(string filePath, string originalContent, string modifiedContent, string? methodSignature = null, string[]? affectedVariables = null)
    {
        var originalLines = originalContent.Split('\n');
        var modifiedLines = modifiedContent.Split('\n');
        
        var changes = new List<Models.TextChange>();
        
        // Simple diff algorithm to find changes
        var diffResult = GenerateLineDiff(originalLines, modifiedLines);
        
        foreach (var change in diffResult)
        {
            changes.Add(change);
        }
        
        return new RefactoringDelta(
            filePath,
            changes,
            methodSignature,
            affectedVariables ?? Array.Empty<string>());
    }
    
    private static List<Models.TextChange> GenerateLineDiff(string[] originalLines, string[] modifiedLines)
    {
        var changes = new List<Models.TextChange>();
        var originalIndex = 0;
        var modifiedIndex = 0;
        
        while (originalIndex < originalLines.Length || modifiedIndex < modifiedLines.Length)
        {
            if (originalIndex >= originalLines.Length)
            {
                // Rest are insertions
                var insertedLines = modifiedLines.Skip(modifiedIndex).ToArray();
                if (insertedLines.Length > 0)
                {
                    changes.Add(new Models.TextChange(
                        originalIndex,
                        originalIndex,
                        "",
                        string.Join("\n", insertedLines),
                        ChangeType.Insert));
                }
                break;
            }
            
            if (modifiedIndex >= modifiedLines.Length)
            {
                // Rest are deletions
                var deletedLines = originalLines.Skip(originalIndex).ToArray();
                if (deletedLines.Length > 0)
                {
                    changes.Add(new Models.TextChange(
                        originalIndex,
                        originalLines.Length - 1,
                        string.Join("\n", deletedLines),
                        "",
                        ChangeType.Delete));
                }
                break;
            }
            
            var originalLine = originalLines[originalIndex].Trim();
            var modifiedLine = modifiedLines[modifiedIndex].Trim();
            
            if (originalLine == modifiedLine)
            {
                // Lines match, move forward
                originalIndex++;
                modifiedIndex++;
            }
            else
            {
                // Find the extent of the difference
                var changeStartOriginal = originalIndex;
                var changeStartModified = modifiedIndex;
                
                // Look ahead to find where they sync up again
                var syncFound = false;
                var lookAhead = 1;
                
                while (lookAhead <= Math.Min(5, Math.Min(originalLines.Length - originalIndex, modifiedLines.Length - modifiedIndex)))
                {
                    if (originalIndex + lookAhead < originalLines.Length && 
                        modifiedIndex + lookAhead < modifiedLines.Length &&
                        originalLines[originalIndex + lookAhead].Trim() == modifiedLines[modifiedIndex + lookAhead].Trim())
                    {
                        syncFound = true;
                        break;
                    }
                    lookAhead++;
                }
                
                if (syncFound)
                {
                    // Replace the differing section
                    var originalSection = string.Join("\n", originalLines.Skip(originalIndex).Take(lookAhead));
                    var modifiedSection = string.Join("\n", modifiedLines.Skip(modifiedIndex).Take(lookAhead));
                    
                    changes.Add(new Models.TextChange(
                        originalIndex,
                        originalIndex + lookAhead - 1,
                        originalSection,
                        modifiedSection,
                        ChangeType.Replace));
                    
                    originalIndex += lookAhead;
                    modifiedIndex += lookAhead;
                }
                else
                {
                    // Couldn't find sync point, treat as replacement
                    var originalSection = originalLines[originalIndex];
                    var modifiedSection = modifiedLines[modifiedIndex];
                    
                    changes.Add(new Models.TextChange(
                        originalIndex,
                        originalIndex,
                        originalSection,
                        modifiedSection,
                        ChangeType.Replace));
                    
                    originalIndex++;
                    modifiedIndex++;
                }
            }
        }
        
        return changes;
    }
    
    public static int EstimateTokenSavings(RefactoringDelta delta, string originalContent)
    {
        var originalTokens = EstimateTokenCount(originalContent);
        var deltaTokens = EstimateTokenCount(string.Join("\n", delta.Changes.Select(c => c.NewText)));
        
        return Math.Max(0, originalTokens - deltaTokens);
    }
    
    private static int EstimateTokenCount(string text)
    {
        // Simple heuristic: ~4 characters per token on average for code
        return text.Length / 4;
    }
}
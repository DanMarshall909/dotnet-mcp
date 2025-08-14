# Task 008: Code Modification Tools

## Priority: 🚀 Feature
**Status**: Pending  
**Estimated Effort**: 12 hours  
**Dependencies**: Task 001, Task 003

## Problem Description

The MCP currently provides analysis and refactoring capabilities but lacks direct code modification tools. Developers need the ability to insert, update, and delete methods/classes safely and intelligently.

### Current Limitations
- No direct code insertion capabilities
- No method/class updating tools
- No safe code deletion
- Limited to analysis and existing refactoring

### Desired Capabilities
- Insert new methods, classes, properties with proper placement
- Update existing code elements while preserving structure
- Safe deletion with dependency checking
- Bulk modifications across multiple files
- Integration with existing refactoring tools

## Solution Design

### 1. Code Modification Framework
```csharp
public interface ICodeModificationService
{
    Task<ModificationResult> InsertCodeAsync(InsertCodeRequest request);
    Task<ModificationResult> UpdateCodeAsync(UpdateCodeRequest request);
    Task<ModificationResult> DeleteCodeAsync(DeleteCodeRequest request);
    Task<ModificationResult> BulkModifyAsync(BulkModificationRequest request);
}

public record ModificationResult
{
    public bool Success { get; init; }
    public string[] ModifiedFiles { get; init; }
    public CodeDelta[] Changes { get; init; }
    public ModificationError[] Errors { get; init; }
    public ConflictResolution[] Conflicts { get; init; }
}
```

### 2. Insert Operations
```csharp
public record InsertCodeRequest
{
    public string FilePath { get; init; }
    public string TargetClass { get; init; }
    public InsertionPoint InsertionPoint { get; init; }
    public CodeElement Element { get; init; }
    public InsertionOptions Options { get; init; }
}

public enum InsertionPoint
{
    BeginningOfClass,
    EndOfClass,
    AfterMethod,
    BeforeMethod,
    AfterProperty,
    BeforeProperty,
    CustomPosition
}

public abstract record CodeElement
{
    public string Name { get; init; }
    public AccessModifier AccessModifier { get; init; }
    public string[] Attributes { get; init; }
    public string Documentation { get; init; }
}

public record MethodElement : CodeElement
{
    public string ReturnType { get; init; }
    public Parameter[] Parameters { get; init; }
    public string Body { get; init; }
    public bool IsAsync { get; init; }
    public bool IsStatic { get; init; }
}

public record PropertyElement : CodeElement
{
    public string Type { get; init; }
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public string GetterBody { get; init; }
    public string SetterBody { get; init; }
}

public record ClassElement : CodeElement
{
    public string BaseClass { get; init; }
    public string[] Interfaces { get; init; }
    public string Namespace { get; init; }
    public CodeElement[] Members { get; init; }
}
```

### 3. Update Operations
```csharp
public record UpdateCodeRequest
{
    public string FilePath { get; init; }
    public string TargetElement { get; init; } // Method/property/class name
    public UpdateStrategy Strategy { get; init; }
    public string NewImplementation { get; init; }
    public bool PreserveSignature { get; init; }
    public bool UpdateDocumentation { get; init; }
}

public enum UpdateStrategy
{
    ReplaceBody,          // Keep signature, replace body
    ReplaceSignature,     // Keep name, replace signature and body
    ReplaceComplete,      // Replace entire element
    ModifyInPlace,        // Semantic modifications
    SmartMerge           // Intelligent merging of changes
}
```

### 4. Delete Operations
```csharp
public record DeleteCodeRequest
{
    public string FilePath { get; init; }
    public string TargetElement { get; init; }
    public DeleteStrategy Strategy { get; init; }
    public bool CheckDependencies { get; init; }
    public bool SafeMode { get; init; }
}

public enum DeleteStrategy
{
    RemoveElement,        // Remove just the element
    RemoveWithUsings,     // Remove element and unused usings
    RemoveWithTests,      // Remove element and associated tests
    SafeRemoval          // Only remove if no dependencies
}
```

## Implementation Steps

1. **Create Code Analysis Foundation**
   ```csharp
   - Roslyn-based code structure analysis
   - Dependency tracking for safe modifications
   - Code placement intelligence
   - Conflict detection logic
   ```

2. **Implement Insertion Logic**
   ```csharp
   - Smart insertion point detection
   - Code formatting and style preservation
   - Using directive management
   - Namespace handling
   ```

3. **Add Update Capabilities**
   ```csharp
   - Signature preservation logic
   - Smart merging algorithms
   - Documentation updates
   - Breaking change detection
   ```

4. **Create Deletion Safety**
   ```csharp
   - Dependency analysis
   - Reference counting
   - Safe removal validation
   - Orphaned code cleanup
   ```

5. **Add MCP Tools**
   ```csharp
   - insert_method, insert_class, insert_property
   - update_method, update_class, update_property  
   - delete_method, delete_class, delete_property
   - bulk_modify: Multi-file modifications
   ```

## Smart Code Placement

### Method Insertion
```csharp
public class SmartMethodInserter
{
    public InsertionLocation FindBestInsertionPoint(ClassDeclarationSyntax targetClass, MethodElement newMethod)
    {
        // Group methods by purpose/accessibility
        var methodGroups = GroupMethodsByPurpose(targetClass.Members);
        
        // Find best group for new method
        var targetGroup = DetermineTargetGroup(newMethod, methodGroups);
        
        // Find specific position within group
        return FindInsertionPointInGroup(targetGroup, newMethod);
    }
    
    private MethodGroup DetermineTargetGroup(MethodElement method, MethodGroup[] groups)
    {
        // Constructors first
        if (method.Name == "ctor") return groups.FirstOrDefault(g => g.Type == GroupType.Constructors);
        
        // Public methods together
        if (method.AccessModifier == AccessModifier.Public) 
            return groups.FirstOrDefault(g => g.Type == GroupType.PublicMethods);
        
        // Similar patterns for private, protected, etc.
        return groups.FirstOrDefault(g => g.Type == GroupType.PrivateMethods);
    }
}
```

### Class Organization
```csharp
public class ClassOrganizer
{
    private readonly string[] _memberOrder = 
    {
        "Fields",
        "Constants", 
        "Constructors",
        "Properties",
        "PublicMethods",
        "ProtectedMethods",
        "PrivateMethods",
        "NestedTypes"
    };
    
    public SyntaxNode OrganizeClass(ClassDeclarationSyntax classDeclaration)
    {
        var organizedMembers = classDeclaration.Members
            .GroupBy(GetMemberCategory)
            .OrderBy(g => Array.IndexOf(_memberOrder, g.Key))
            .SelectMany(g => g.OrderBy(GetMemberName))
            .ToArray();
        
        return classDeclaration.WithMembers(SyntaxFactory.List(organizedMembers));
    }
}
```

## MCP Tool Definitions

### 1. Insert Method Tool
```json
{
  "name": "insert_method",
  "description": "Insert a new method into a class with smart placement",
  "inputSchema": {
    "type": "object",
    "properties": {
      "filePath": { "type": "string" },
      "className": { "type": "string" },
      "methodName": { "type": "string" },
      "returnType": { "type": "string" },
      "parameters": { "type": "array" },
      "body": { "type": "string" },
      "accessModifier": { "type": "string" },
      "isAsync": { "type": "boolean" },
      "isStatic": { "type": "boolean" },
      "insertionPoint": { "type": "string" },
      "documentation": { "type": "string" }
    }
  }
}
```

### 2. Update Method Tool
```json
{
  "name": "update_method", 
  "description": "Update an existing method's implementation",
  "inputSchema": {
    "type": "object",
    "properties": {
      "filePath": { "type": "string" },
      "className": { "type": "string" },
      "methodName": { "type": "string" },
      "newImplementation": { "type": "string" },
      "updateStrategy": { "type": "string" },
      "preserveSignature": { "type": "boolean" },
      "updateDocumentation": { "type": "boolean" }
    }
  }
}
```

### 3. Delete Code Tool
```json
{
  "name": "delete_code",
  "description": "Safely delete methods, classes, or properties",
  "inputSchema": {
    "type": "object", 
    "properties": {
      "filePath": { "type": "string" },
      "targetElement": { "type": "string" },
      "elementType": { "type": "string" },
      "deleteStrategy": { "type": "string" },
      "checkDependencies": { "type": "boolean" },
      "safeMode": { "type": "boolean" }
    }
  }
}
```

### 4. Bulk Modify Tool
```json
{
  "name": "bulk_modify",
  "description": "Apply modifications across multiple files",
  "inputSchema": {
    "type": "object",
    "properties": {
      "modifications": { "type": "array" },
      "projectPath": { "type": "string" },
      "dryRun": { "type": "boolean" },
      "createBackup": { "type": "boolean" }
    }
  }
}
```

## Safety and Conflict Resolution

### Dependency Checking
```csharp
public class DependencyChecker
{
    public async Task<DependencyAnalysis> AnalyzeDependenciesAsync(string filePath, string elementName)
    {
        var references = await FindAllReferencesAsync(filePath, elementName);
        
        return new DependencyAnalysis
        {
            InternalReferences = references.Where(r => r.IsInternalToProject).ToArray(),
            ExternalReferences = references.Where(r => !r.IsInternalToProject).ToArray(),
            TestReferences = references.Where(r => r.IsInTestProject).ToArray(),
            CanSafelyDelete = !references.Any(r => r.IsEssential)
        };
    }
}
```

### Conflict Resolution
```csharp
public class ConflictResolver
{
    public async Task<ResolutionStrategy> ResolveConflictAsync(ModificationConflict conflict)
    {
        return conflict.Type switch
        {
            ConflictType.DuplicateName => await ResolveDuplicateNameAsync(conflict),
            ConflictType.SignatureMismatch => await ResolveSignatureMismatchAsync(conflict),
            ConflictType.DependencyViolation => await ResolveDependencyViolationAsync(conflict),
            _ => ResolutionStrategy.UserIntervention
        };
    }
}
```

## Acceptance Criteria

- [ ] Insert methods/classes/properties with smart placement
- [ ] Update existing code while preserving structure
- [ ] Safe deletion with dependency checking
- [ ] Bulk modifications across multiple files
- [ ] Conflict detection and resolution
- [ ] Integration with existing refactoring tools
- [ ] Maintains code formatting and style
- [ ] Comprehensive error handling and rollback

## Files to Create/Modify

**New Files:**
- `src/DotNetMcp.Core/CodeModification/`
  - `ICodeModificationService.cs`
  - `SmartMethodInserter.cs`
  - `ClassOrganizer.cs`
  - `DependencyChecker.cs`
  - `ConflictResolver.cs`
  - `CodePlacementService.cs`

- `src/DotNetMcp.Core/Features/CodeModification/`
  - `InsertMethodHandler.cs`
  - `UpdateMethodHandler.cs`
  - `DeleteCodeHandler.cs`
  - `BulkModifyHandler.cs`

**Modified Files:**
- VSA service registration
- MCP server tool definitions
- Integration with existing refactoring services

## Testing Strategy

- Unit tests for insertion/update/delete logic
- Integration tests with real codebases
- Conflict resolution scenario testing
- Performance tests with large files
- Code style preservation validation

## Success Metrics

- 100% successful code insertions without syntax errors
- Smart placement accuracy >90%
- Zero data loss during modifications
- Conflict resolution success rate >95%
- Performance <2 seconds for single file modifications
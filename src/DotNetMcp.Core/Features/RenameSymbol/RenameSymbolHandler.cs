using DotNetMcp.Core.Common;
using DotNetMcp.Core.Refactoring;
using DotNetMcp.Core.Services;
using DotNetMcp.Core.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.RenameSymbol;

/// <summary>
/// Handler for rename symbol command using VSA pattern
/// </summary>
public class RenameSymbolHandler : BaseHandler<RenameSymbolCommand, RenameSymbolResponse>
{
    private readonly RenameSymbolRefactorer _singleFileRefactorer;
    private readonly MultiFileRefactoringEngine _multiFileEngine;
    private readonly IValidator<RenameSymbolCommand> _validator;

    public RenameSymbolHandler(
        ILogger<RenameSymbolHandler> logger,
        RenameSymbolRefactorer singleFileRefactorer,
        MultiFileRefactoringEngine multiFileEngine,
        IValidator<RenameSymbolCommand> validator) : base(logger)
    {
        _singleFileRefactorer = singleFileRefactorer;
        _multiFileEngine = multiFileEngine;
        _validator = validator;
    }

    protected override async Task<Result<Unit>> ValidateAsync(RenameSymbolCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Unit>.Failure($"Validation failed: {errors}");
        }

        return Result<Unit>.Success(Unit.Value);
    }

    protected override async Task<Result<RenameSymbolResponse>> HandleAsync(
        RenameSymbolCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.MultiFile)
            {
                return await HandleMultiFileRename(request);
            }
            else
            {
                return await HandleSingleFileRename(request);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to rename symbol {OldName} to {NewName}", request.OldName, request.NewName);
            return Result<RenameSymbolResponse>.Failure("Failed to rename symbol", ex);
        }
    }

    private async Task<Result<RenameSymbolResponse>> HandleSingleFileRename(RenameSymbolCommand request)
    {
        var code = await File.ReadAllTextAsync(request.FilePath);
        var result = await _singleFileRefactorer.RenameSymbolAsync(
            code, 
            request.OldName, 
            request.NewName, 
            request.SymbolType);

        var response = new RenameSymbolResponse
        {
            Success = result.TotalChanges > 0,
            ModifiedCode = result.ModifiedCode,
            ErrorMessage = result.Conflicts.Length > 0 ? string.Join(", ", result.Conflicts) : null,
            AffectedFiles = new[] { request.FilePath },
            ChangesCount = result.TotalChanges
        };

        return Result<RenameSymbolResponse>.Success(response);
    }

    private async Task<Result<RenameSymbolResponse>> HandleMultiFileRename(RenameSymbolCommand request)
    {
        if (string.IsNullOrEmpty(request.SolutionPath))
        {
            return Result<RenameSymbolResponse>.Failure("Solution path is required for multi-file operations");
        }

        // Load solution or project
        bool loaded;
        if (request.SolutionPath.EndsWith(".sln") || request.SolutionPath.EndsWith(".csproj"))
        {
            loaded = await _multiFileEngine.LoadSolutionAsync(request.SolutionPath);
        }
        else
        {
            // Assume it's a directory with files
            var files = Directory.GetFiles(request.SolutionPath, "*.cs", SearchOption.AllDirectories);
            loaded = await _multiFileEngine.LoadFilesAsync(files);
        }

        if (!loaded)
        {
            return Result<RenameSymbolResponse>.Failure("Failed to load solution or files");
        }

        try
        {
            var deltas = await _multiFileEngine.RenameSymbolAcrossFilesAsync(
                request.OldName, 
                request.NewName, 
                request.FilePath);

            var affectedFiles = deltas.Select(d => d.FilePath).Distinct().ToArray();
            var totalChanges = deltas.Sum(d => d.Changes.Count);

            // For simplicity, return the first file's modified code
            // In a real implementation, you might want to return all changes
            var modifiedCode = deltas.FirstOrDefault()?.Changes.FirstOrDefault()?.NewText ?? "";

            var response = new RenameSymbolResponse
            {
                Success = deltas.Any(),
                ModifiedCode = modifiedCode,
                AffectedFiles = affectedFiles,
                ChangesCount = totalChanges,
                ErrorMessage = deltas.Any() ? null : $"Symbol '{request.OldName}' not found"
            };

            return Result<RenameSymbolResponse>.Success(response);
        }
        finally
        {
            _multiFileEngine.Dispose();
        }
    }
}
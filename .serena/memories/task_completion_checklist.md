# Task Completion Checklist

When completing a development task on the NotNow project, ensure you follow these steps:

## Before Committing Code

### 1. Build Verification
```bash
# Clean build to ensure no cached issues
dotnet clean
dotnet build

# Build in Release mode to catch additional issues
dotnet build -c Release
```

### 2. Code Formatting
```bash
# Format code if dotnet-format is installed
dotnet format

# Or manually ensure:
# - Consistent indentation (4 spaces)
# - No trailing whitespace
# - Consistent brace placement
```

### 3. Manual Testing
- Run the console application: `cd NotNow.Console && dotnet run`
- Test the specific feature/fix you implemented
- Verify no regressions in related functionality
- Test error cases and edge conditions

### 4. Check for Common Issues

#### Null Reference Checks
- Ensure null checks where needed
- Leverage nullable reference type warnings
- Use null-conditional operators appropriately

#### Async/Await
- No `.Result` or `.Wait()` calls
- All async methods end with `Async`
- Proper exception handling in async methods

#### String Handling
- Use `string.IsNullOrEmpty()` or `string.IsNullOrWhiteSpace()`
- Use string interpolation over concatenation
- Handle special characters in user input

#### Resource Management
- Dispose of IDisposable objects (using statements)
- Close connections properly
- Clean up event handlers

### 5. Configuration Files
- Ensure `appsettings.json` is not committed (it's in .gitignore)
- Update `appsettings.example.json` if new settings added
- Document any new configuration requirements

### 6. Documentation Updates
- Update README.md if:
  - New commands added
  - Setup process changed
  - New dependencies introduced
- Update inline comments for complex logic
- Ensure command help text is accurate

## GitHub Integration Checks

### For GitHub-Related Changes
- Verify API calls handle errors gracefully
- Check rate limiting is respected
- Ensure PAT permissions are documented
- Test with different issue states

### For Command Changes
- Verify command parsing works correctly
- Test command in both issue body and comments (as appropriate)
- Ensure command results format properly
- Check command appears in help/autocomplete

## Final Verification

### Console Application Specific
- Test autocomplete functionality
- Verify console navigation works
- Check display formatting on different window sizes
- Ensure no console color issues

### Command Framework
- New commands registered properly
- Command handlers return appropriate `CommandResult`
- Command validation works
- Error messages are user-friendly

## Git Commit

### Commit Message Format
```
type: brief description

- Detailed change 1
- Detailed change 2

Fixes #issue-number (if applicable)
```

Types: feat, fix, refactor, docs, test, chore

### Pre-Push Checklist
- [ ] Code builds without warnings
- [ ] Manual testing completed
- [ ] No sensitive data in code
- [ ] No debug/console output left in
- [ ] Documentation updated if needed
- [ ] Follows code style conventions

## Known Issues to Watch For

1. **Autocomplete Display**: Ensure cursor positioning is correct
2. **Command Parsing**: Verify regex patterns work with various formats
3. **State Management**: Check IssueStateService properly maintains state
4. **GitHub API**: Handle rate limiting and connection errors
5. **Console Input**: Test special characters and long inputs

## Tools to Consider Using

- **Visual Studio Code**: Built-in debugging and IntelliSense
- **Visual Studio**: Full IDE features and profiling
- **dotnet-format**: Automatic code formatting
- **GitHub CLI (`gh`)**: Test GitHub integration locally

## When in Doubt

- Check existing code patterns in similar files
- Review the CommandHandler implementations for examples
- Ensure consistency with existing module structure
- Test edge cases and error conditions thoroughly
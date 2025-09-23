# NotNow Application Design Documentation

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Component Structure](#component-structure)
3. [Core Classes](#core-classes)
4. [Issue State Management](#issue-state-management)
5. [Command Execution Flow](#command-execution-flow)
6. [UI Event Flows](#ui-event-flows)
7. [Data Synchronization](#data-synchronization)
8. [Filter and Search Operations](#filter-and-search-operations)

## Architecture Overview

### High-Level Component Diagram

```plantuml
@startuml
!theme plain

package "NotNow.Quake" {
    [TerminalPage UI] as UI
    [IssueItem Model] as IssueItem
}

package "NotNow.Core" {
    [IssueStateParser] as Parser
    [IssueStateManager] as StateManager
    [ModularCommandParser] as CommandParser
    [CommandExecutor] as Executor
    [CommandModules] as Modules
}

package "NotNow.GitHubService" {
    [GitHubService] as GitHub
    [CommandPostingService] as Posting
}

package "External" {
    [GitHub API] as API
    [Octokit.NET] as Octokit
}

UI --> IssueItem : displays
UI --> Parser : parses state
UI --> StateManager : manages versions
UI --> CommandParser : parses commands
UI --> Executor : executes
UI --> GitHub : fetches/updates
UI --> Posting : posts commands

Parser --> StateManager : uses
Executor --> Modules : invokes
GitHub --> Octokit : uses
Octokit --> API : calls
Posting --> GitHub : uses

@enduml
```

## Component Structure

### Service Dependencies

```plantuml
@startuml
!theme plain

class TerminalPage {
    - _gitHubService: IGitHubService
    - _gitHubServiceManager: IGitHubServiceManager
    - _issueStateParser: IIssueStateParser
    - _stateManager: IIssueStateManager
    - _stateService: IssueStateService
    - _commandParser: ICommandParser
    - _commandExecutor: ICommandExecutor
    - _commandPostingService: ICommandPostingService
    - _configuration: IConfiguration
    --
    + LoadIssuesAsync()
    + LoadIssueDetails(issueNumber)
    + PostCommandAndUpdateState()
    + UpdateEmbeddedStateAfterCommand()
}

interface IGitHubService {
    + GetIssuesAsync(filter): Task<IReadOnlyList<Issue>>
    + GetIssueAsync(number): Task<Issue>
    + GetIssueCommentsAsync(number): Task<IReadOnlyList<IssueComment>>
    + CreateIssueAsync(title, body): Task<Issue>
    + UpdateIssueAsync(number, title, body, state): Task<Issue>
    + AddCommentToIssueAsync(number, comment): Task<IssueComment>
    + CloseIssueAsync(number): Task<Issue>
    + ReopenIssueAsync(number): Task<Issue>
    + GetCurrentUserAsync(): Task<User>
}

interface IIssueStateParser {
    + ParseIssueState(issue, comments): IssueState
    + ParseVersionedState(issue): IssueStateVersion?
    + ParseCommandIntoState(state, command, timestamp): IssueState
}

interface IIssueStateManager {
    + ExtractStateFromBody(body): IssueStateVersion?
    + EmbedStateInBody(body, state): string
    + CreateNewVersion(state, command, clientId): IssueStateVersion
    + IncrementVersion(current, newState, command, clientId): IssueStateVersion
    + IsStateStale(state, maxAge): bool
}

TerminalPage ..> IGitHubService : uses
TerminalPage ..> IIssueStateParser : uses
TerminalPage ..> IIssueStateManager : uses

@enduml
```

## Core Classes

### Issue State Models

```plantuml
@startuml
!theme plain

class IssueState {
    + IssueNumber: int
    + Title: string
    + Status: string
    + Priority: string
    + Type: string
    + Assignee: string
    + Estimate: string
    + DueDate: DateTime?
    + Tags: List<string>
    + Subtasks: List<Subtask>
    + Sessions: List<WorkSession>
    + TotalTimeSpent: TimeSpan
    + LastUpdated: DateTime
    + IsInitialized: bool
    + ActiveSession: WorkSession?
}

class IssueStateVersion {
    + {static} StateBeginMarker: string
    + {static} StateEndMarker: string
    + {static} CurrentSchemaVersion: string
    + SchemaVersion: string
    + StateVersion: int
    + LastUpdated: DateTime
    + LastUpdatedBy: string
    + LastCommand: string
    + Data: IssueStateData
}

class IssueStateData {
    + IssueNumber: int
    + Title: string
    + Status: string
    + Priority: string
    + Type: string
    + Assignee: string
    + Estimate: string
    + DueDate: DateTime?
    + Tags: List<string>
    + Subtasks: List<Subtask>
    + Sessions: List<WorkSession>
    + TotalTimeSpent: string
    + IsInitialized: bool
    + GetTaskCounts(): (Open: int, Total: int)
}

class Subtask {
    + Id: string
    + Title: string
    + Status: string
    + Estimate: string
    + Assignee: string
    + CompletedAt: DateTime?
}

class WorkSession {
    + Id: string
    + StartedAt: DateTime
    + EndedAt: DateTime?
    + Duration: TimeSpan
    + Description: string
    + User: string
}

class IssueItem {
    + Number: int
    + Title: string
    + DisplayText: string
    + IsClosed: bool
    + IsSelected: bool
    + Status: string
    + Priority: string
    + DueDate: DateTime?
    + OpenTaskCount: int
    + TotalTaskCount: int
    + Tags: List<string>
    --
    + TextColor: Color
    + TextDecorations: TextDecorations
    + CheckboxText: string
    + StatusIcon: string
    + CriticalPriorityIcon: string
    + DueDateIcon: string
    + TaskCountIcon: string
}

IssueStateVersion --> IssueStateData : contains
IssueStateData --> Subtask : contains *
IssueStateData --> WorkSession : contains *
IssueState --> Subtask : contains *
IssueState --> WorkSession : contains *

@enduml
```

## Issue State Management

### State Resolution Flow

```plantuml
@startuml
!theme plain

start
:Load Issue from GitHub;

if (Issue body contains embedded state?) then (yes)
    :Extract IssueStateVersion from body;

    if (State is stale? > 5 mins) then (yes)
        :Parse state from comments;
        note right
            Fallback to comment parsing
            for stale embedded state
        end note
    else (no)
        :Use embedded state;
        :Convert IssueStateData to IssueState;
        note right
            Fast path - no need to
            fetch or parse comments
        end note
    endif
else (no)
    :Fetch all issue comments;
    :Parse commands from issue body;
    note right
        IMPORTANT: Remove embedded state
        section before parsing to avoid
        parsing lastCommand as real command
    end note
    :Parse commands from comments chronologically;
    :Build IssueState from commands;
endif

:Display state in UI;
stop

@enduml
```

### Embedded State Update Process

```plantuml
@startuml
!theme plain

participant "UI" as UI
participant "CommandExecutor" as Exec
participant "PostingService" as Post
participant "StateManager" as SM
participant "GitHub API" as API

UI -> Exec: Execute command
activate Exec
Exec -> Exec: Process command
Exec --> UI: ExecutionResult
deactivate Exec

UI -> Post: PostCommandToGitHub(command, result)
activate Post
Post -> API: Add comment with command
Post --> UI: Comment posted
deactivate Post

UI -> UI: UpdateEmbeddedStateAfterCommand()
activate UI

UI -> API: GetIssueAsync(number)
API --> UI: Current issue

UI -> SM: ExtractStateFromBody(issue.Body)
SM --> UI: Current IssueStateVersion

alt Has embedded state
    UI -> UI: ConvertVersionedToState()
    UI -> UI: ParseCommandIntoState(state, command)
    note right
        Apply command to current state
        to get new state
    end note
    UI -> SM: IncrementVersion(current, newState, command)
else No embedded state
    UI -> API: GetIssueCommentsAsync()
    API --> UI: Comments
    UI -> UI: ParseIssueState(issue, comments)
    UI -> SM: CreateNewVersion(state, command)
end

SM --> UI: New IssueStateVersion

UI -> SM: EmbedStateInBody(issue.Body, newVersion)
SM --> UI: Updated body with embedded state

UI -> API: UpdateIssueAsync(body: updatedBody)
API --> UI: Issue updated

deactivate UI

@enduml
```

## Command Execution Flow

### Command Processing Pipeline

```plantuml
@startuml
!theme plain

participant "User" as User
participant "TerminalPage" as UI
participant "CommandParser" as Parser
participant "CommandExecutor" as Exec
participant "CommandHandler" as Handler
participant "PostingService" as Post
participant "GitHub" as GH

User -> UI: Enter command/action
activate UI

UI -> UI: Build command string\n"/notnow [command] [args]"

UI -> Parser: Parse(command, context)
activate Parser
Parser -> Parser: Validate syntax
Parser -> Parser: Extract parameters
Parser --> UI: ParseResult with Commands[]
deactivate Parser

UI -> Exec: ExecuteCommandsAsync(commands, context)
activate Exec

loop for each command
    Exec -> Handler: ExecuteAsync(context, args)
    activate Handler
    Handler -> Handler: Process command
    Handler --> Exec: CommandResult
    deactivate Handler
end

Exec --> UI: ExecutionResult
deactivate Exec

alt Command successful
    UI -> UI: PostCommandAndUpdateState()
    activate UI

    UI -> Post: PostCommandToGitHubAsync()
    activate Post
    Post -> GH: Add comment
    deactivate Post

    UI -> UI: UpdateEmbeddedStateAfterCommand()
    UI -> GH: Update issue body with state

    deactivate UI

    UI -> UI: Refresh UI
end

deactivate UI

@enduml
```

### Command Types and Handlers

```plantuml
@startuml
!theme plain

abstract class CommandHandler<T> {
    + {abstract} ExecuteAsync(context, args): Task<CommandResult>
}

class InitCommandHandler {
    + ExecuteAsync(): Initialize tracking
}

class StatusCommandHandler {
    + ExecuteAsync(): Change status
}

class PriorityCommandHandler {
    + ExecuteAsync(): Set priority
}

class AssignCommandHandler {
    + ExecuteAsync(): Assign user
}

class TagsCommandHandler {
    + ExecuteAsync(): Add/remove tags
}

class SubtaskCommandHandler {
    + ExecuteAsync(): Manage subtasks
}

class TimeCommandHandler {
    + ExecuteAsync(): Log time
}

class EstimateCommandHandler {
    + ExecuteAsync(): Set estimate
}

class DueCommandHandler {
    + ExecuteAsync(): Set due date
}

CommandHandler <|-- InitCommandHandler
CommandHandler <|-- StatusCommandHandler
CommandHandler <|-- PriorityCommandHandler
CommandHandler <|-- AssignCommandHandler
CommandHandler <|-- TagsCommandHandler
CommandHandler <|-- SubtaskCommandHandler
CommandHandler <|-- TimeCommandHandler
CommandHandler <|-- EstimateCommandHandler
CommandHandler <|-- DueCommandHandler

@enduml
```

## UI Event Flows

### Issue Loading and Display

```plantuml
@startuml
!theme plain

title Issue Loading Sequence

actor User
participant "TerminalPage" as UI
participant "GitHubService" as GH
participant "IssueStateParser" as Parser
participant "UI Components" as Components

User -> UI: Select repository
activate UI

UI -> UI: ShowLoadingIndicator()
UI -> GH: GetIssuesAsync(Open)
activate GH
GH --> UI: Open issues list
deactivate GH

UI -> GH: GetIssuesAsync(Closed)
activate GH
GH --> UI: Closed issues list
deactivate GH

loop for each issue
    UI -> Parser: ParseVersionedState(issue)
    Parser --> UI: IssueStateVersion

    UI -> UI: Create IssueItem
    note right
        Extract:
        - Task counts
        - Status
        - Priority
        - Due date
        - Tags
    end note

    UI -> UI: Apply filters
    note right
        - Priority filters
        - Closed issues filter
        - Text filter
    end note
end

UI -> Components: Update _filteredIssues
UI -> UI: HideLoadingIndicator()
UI -> Components: Display filtered issues

User -> UI: Select issue
UI -> UI: LoadIssueDetails()

deactivate UI

@enduml
```

### Tag Management Flow

```plantuml
@startuml
!theme plain

title Tag Add/Remove Flow

actor User
participant "UI" as UI
participant "Parser" as Parser
participant "Executor" as Exec
participant "GitHub" as GH
participant "StateManager" as SM

== Add Tag ==
User -> UI: Click Add Tag
UI -> UI: Show TagInputPanel
User -> UI: Enter tag name
User -> UI: Submit

UI -> UI: Build command\n"/notnow tags add {tag}"
UI -> Parser: Parse command
Parser --> UI: ParseResult

UI -> Exec: ExecuteCommandsAsync()
Exec --> UI: Success

UI -> UI: PostCommandAndUpdateState()
note right
    1. Post command to GitHub
    2. Update embedded state
    3. Note: Fixed duplicate call issue
end note

UI -> GH: UpdateIssueAsync(with embedded state)
UI -> UI: Hide TagInputPanel
UI -> UI: LoadIssueDetails() to refresh

== Remove Tag ==
User -> UI: Click X on tag
UI -> UI: Build command\n"/notnow tags remove {tag}"
UI -> Parser: Parse command
UI -> Exec: ExecuteCommandsAsync()
UI -> UI: PostCommandAndUpdateState()
UI -> UI: LoadIssueDetails() to refresh

@enduml
```

### Filter Operations

```plantuml
@startuml
!theme plain

title Issue Filtering System

actor User
participant "UI" as UI
participant "Filter Logic" as Filter

== Text Filter ==
User -> UI: Type in filter textbox
UI -> UI: OnIssueFilterTextChanged(text)
UI -> Filter: ApplyFilter()

activate Filter
Filter -> Filter: Clear _filteredIssues
loop for each issue in _allIssues
    Filter -> Filter: Check title contains text
    Filter -> Filter: Check tags contain text
    alt matches filter
        Filter -> Filter: Add to _filteredIssues
    end
end
deactivate Filter

UI -> UI: Update CollectionView
UI -> UI: Select first item if any

== Priority Filter ==
User -> UI: Click priority filter button
UI -> UI: Toggle filter state
UI -> UI: UpdatePriorityFilterVisual()
UI -> UI: ApplyPriorityFilters()
UI -> UI: LoadIssuesAsync()
note right
    Reload issues with
    new priority filter
end note

== Closed Issues Toggle ==
User -> UI: Click closed issues toggle
UI -> UI: Toggle _hideClosedIssues
UI -> UI: UpdateToggleVisual()
UI -> UI: LoadIssuesAsync()
note right
    Reload to include/exclude
    closed issues
end note

@enduml
```

## Data Synchronization

### State Synchronization Between UI and GitHub

```plantuml
@startuml
!theme plain

title State Synchronization Architecture

package "GitHub Issue" {
    component "Issue Body" as Body {
        [Description Text]
        [Embedded State JSON] #lightblue
    }

    component "Issue Comments" as Comments {
        [User Comments]
        [Command Comments] #lightgreen
    }
}

package "NotNow Application" {
    component "State Parser" as Parser {
        [Extract Embedded State]
        [Parse Commands]
        [Build State]
    }

    component "State Manager" as Manager {
        [Version Control]
        [Conflict Resolution]
        [State Embedding]
    }

    component "UI State" as UIState {
        [Current IssueState]
        [Display Models]
        [Filtered Views]
    }
}

Body --> Parser : Read embedded state
Comments --> Parser : Parse commands
Parser --> UIState : Provide state

UIState --> Manager : Update state
Manager --> Body : Embed new version

note bottom of Manager
    Version tracking prevents
    duplicate command execution
end note

note right of Parser
    Critical fix: Remove embedded
    state section before parsing
    to avoid executing lastCommand
end note

@enduml
```

### Command Posting and State Update

```plantuml
@startuml
!theme plain

title Command Post and State Update Timing

participant "User Action" as User
participant "UI Thread" as UI
participant "Command Executor" as Exec
participant "GitHub API" as API
participant "State Manager" as SM

User -> UI: Trigger command
activate UI

UI -> Exec: Execute locally
activate Exec
Exec --> UI: Local result
deactivate Exec

par Post to GitHub
    UI -> API: Post command comment
    activate API
    API --> UI: Comment created
    deactivate API
and Update embedded state
    UI -> SM: Update state version
    activate SM
    SM -> API: Get current issue
    API --> SM: Issue data
    SM -> SM: Apply command to state
    SM -> SM: Increment version
    SM -> API: Update issue body
    API --> SM: Updated
    SM --> UI: State updated
    deactivate SM
end

UI -> UI: Wait(PostCommandDelay)
note right
    Default 1000ms delay to
    ensure GitHub processes
end note

UI -> UI: Reload issue details
UI -> API: Fetch updated issue
API --> UI: Latest state

deactivate UI

@enduml
```

## Filter and Search Operations

### Issue List Filtering Architecture

```plantuml
@startuml
!theme plain

class FilterSystem {
    - _filterText: string
    - _hideClosedIssues: bool
    - _filterCriticalPriority: bool
    - _filterHighPriority: bool
    - _filterMediumPriority: bool
    - _filterLowPriority: bool
    --
    + ApplyFilter(): void
    + ApplyPriorityFilters(): Task
}

class IssueCollections {
    - _issues: ObservableCollection<IssueItem>
    - _filteredIssues: ObservableCollection<IssueItem>
    - _allIssues: List<IssueItem>
}

note left of IssueCollections
    _issues: Raw from GitHub with priority filter
    _allIssues: Copy for filtering
    _filteredIssues: Display collection
end note

FilterSystem --> IssueCollections : filters

@enduml
```

### Loading Indicator Animation

```plantuml
@startuml
!theme plain

state "Idle" as Idle
state "Loading" as Loading {
    state "Show Indicator" as Show
    state "Rotate Animation" as Rotate
    state "Cancel Token Active" as Cancel

    Show --> Rotate : Start animation
    Rotate --> Rotate : Loop 333ms rotation
    Cancel --> Idle : Hide indicator
}

Idle --> Show : ShowLoadingIndicator()
Loading --> Cancel : HideLoadingIndicator()

note right of Rotate
    Fixed: Run in Task.Run
    to avoid UI freeze
end note

@enduml
```

## Known Issues and Fixes

### Issue: Duplicate Tags with Escaped Quotes
**Problem**: Tags appeared twice, with one having an escaped quote character.

**Root Cause**: When parsing state from comments (fallback path), the parser was also parsing the `/notnow` command inside the embedded state's `lastCommand` JSON field as a real command.

**Fix**: Modified `ParseIssueState` to remove the embedded state section before parsing commands from the issue body.

```plantuml
@startuml
!theme plain

title Tag Duplication Issue Fix

participant "ParseIssueState" as Parser
participant "CommandRegex" as Regex

group Before Fix
    Parser -> Regex: Parse entire issue body
    note right
        Body contains:
        "lastCommand": "/notnow tags add client:ccs"
        This gets matched and executed!
    end note
    Regex --> Parser: Found 2 commands\n(real + from JSON)
end

group After Fix
    Parser -> Parser: Remove embedded state section
    Parser -> Regex: Parse cleaned body
    Regex --> Parser: Found 1 command (real only)
end

@enduml
```

### Issue: Duplicate UpdateEmbeddedStateAfterCommand Calls
**Problem**: State was being updated twice, causing version increments without changes.

**Root Cause**: `PostCommandAndUpdateState` already calls `UpdateEmbeddedStateAfterCommand`, but some methods were calling it again.

**Fix**: Removed duplicate calls to `UpdateEmbeddedStateAfterCommand`.

## Performance Optimizations

### Embedded State Benefits

```plantuml
@startuml
!theme plain

title Performance: Embedded State vs Comment Parsing

alt With Embedded State (Fast Path)
    UI -> GitHub: Get Issue (1 API call)
    GitHub --> UI: Issue with embedded state
    UI -> UI: Extract and use state
    note right
        No need to fetch comments
        Instant state availability
    end note
else Without Embedded State (Slow Path)
    UI -> GitHub: Get Issue (1 API call)
    GitHub --> UI: Issue without state
    UI -> GitHub: Get All Comments (1 API call)
    GitHub --> UI: Comments list
    UI -> UI: Parse all commands chronologically
    note right
        Additional API call
        CPU intensive parsing
    end note
end

@enduml
```

## Subtask Management

### Subtask Lifecycle

```plantuml
@startuml
!theme plain

title Subtask State Machine

[*] --> Pending : Create
Pending --> InProgress : Start work
InProgress --> Pending : Pause
InProgress --> Done : Complete
Done --> Pending : Reopen
Done --> [*] : Remove

note right of Done
    Complete can include:
    - Time spent
    - Completion notes
end note

@enduml
```

### Subtask Command Processing

```plantuml
@startuml
!theme plain

title Subtask Operations

actor User
participant "UI" as UI
participant "Parser" as Parser
participant "State" as State

== Create Subtask ==
User -> UI: Enter title and estimate
UI -> UI: Generate subtask ID (st1, st2, ...)
UI -> UI: Build command\n"/notnow subtask add 'title' --id st1 --estimate 2h"
UI -> Parser: Parse command
Parser -> State: Add to Subtasks list

== Complete Subtask ==
User -> UI: Check subtask
UI -> UI: Show completion dialog
User -> UI: Enter time spent and notes
UI -> UI: Build command\n"/notnow complete st1 --time 1h30m --notes 'Fixed bug'"
UI -> Parser: Parse command
Parser -> State: Update subtask status
Parser -> State: Add work session

== Reopen Subtask ==
User -> UI: Click reopen
UI -> UI: Build command\n"/notnow reopen st1"
UI -> Parser: Parse command
Parser -> State: Reset status to pending

@enduml
```

## Time Tracking System

### Work Session Management

```plantuml
@startuml
!theme plain

class TimeTrackingSystem {
    - Sessions: List<WorkSession>
    - ActiveSession: WorkSession?
    - TotalTimeSpent: TimeSpan
    --
    + StartSession(description): void
    + StopSession(): void
    + LogTime(duration, description, date): void
    + GetSessionsForWeek(date): List<WorkSession>
    + CalculateDailyTotals(): Dictionary<Date, TimeSpan>
}

class WorkSessionCalendar {
    - WeekStartDate: DateTime
    - DailyTotals: Dictionary<Date, TimeSpan>
    --
    + RenderWeekView(): Grid
    + NavigatePreviousWeek(): void
    + NavigateNextWeek(): void
    + HighlightToday(): void
}

TimeTrackingSystem --> WorkSession : manages
WorkSessionCalendar --> TimeTrackingSystem : displays

@enduml
```

### Time Tracking UI Flow

```plantuml
@startuml
!theme plain

title Time Tracking Calendar Display

participant "LoadIssueDetails" as Load
participant "UpdateTimeTrackingCalendar" as Calendar
participant "WeekRowsContainer" as Container

Load -> Calendar: UpdateTimeTrackingCalendar(state)
activate Calendar

Calendar -> Calendar: Clear existing rows
Calendar -> Calendar: Get week start (Monday)
Calendar -> Calendar: Calculate daily totals

loop for each day of week
    Calendar -> Calendar: Create day column
    Calendar -> Calendar: Add day label
    Calendar -> Calendar: Add hours label

    alt is today
        Calendar -> Calendar: Highlight background
    end

    Calendar -> Container: Add column to grid
end

Calendar -> Calendar: Calculate week total
Calendar -> Container: Add total label

deactivate Calendar

@enduml
```

## Issue Creation Flow

### New Issue with Auto-Commands

```plantuml
@startuml
!theme plain

title Issue Creation with Initialization

actor User
participant "UI" as UI
participant "GitHub" as GH
participant "Commands" as Cmd

User -> UI: Fill issue form
note right
    - Title (required)
    - Description (required)
    - Due Date (optional)
    - Estimate (optional)
end note

User -> UI: Submit

UI -> GH: CreateIssueAsync(title, description)
GH --> UI: New issue created

UI -> Cmd: Post "/notnow init"
UI -> Cmd: Post "/notnow status todo"
UI -> Cmd: Post "/notnow priority medium"

alt Current user available
    UI -> Cmd: Post "/notnow assign {username}"
end

alt Due date provided
    UI -> UI: Validate date format (YYYY-MM-DD)
    UI -> Cmd: Post "/notnow due {date}"
end

alt Estimate provided
    UI -> UI: Validate estimate format (1h30m)
    UI -> Cmd: Post "/notnow estimate {time}"
end

UI -> UI: Wait for processing
UI -> UI: Refresh issues list

@enduml
```

## Status and Priority Management

### Status Button States

```plantuml
@startuml
!theme plain

title Status Management UI

state "Status Buttons" as Buttons {
    state "Todo" as Todo
    state "In Progress" as InProgress
    state "Blocked" as Blocked
    state "Done" as Done

    Todo --> InProgress : Click
    InProgress --> Blocked : Click
    InProgress --> Done : Click
    Blocked --> InProgress : Click
    Done --> Todo : Reopen issue
}

note right of Buttons
    Visual states:
    - Active: Blue background
    - Inactive: Dark background
    - All have blue borders
end note

@enduml
```

### Priority System

```plantuml
@startuml
!theme plain

class PrioritySystem {
    + Critical : "🔴 Urgent"
    + High : "🟡 Important"
    + Medium : "🔵 Normal"
    + Low : "⚪ Low"
}

class PriorityFilter {
    - _filterCriticalPriority: bool
    - _filterHighPriority: bool
    - _filterMediumPriority: bool
    - _filterLowPriority: bool
    --
    + ToggleFilter(priority): void
    + ApplyToIssueList(): void
    + UpdateButtonVisual(): void
}

note bottom of PriorityFilter
    Filters only apply to
    open issues, not closed
end note

@enduml
```

## Repository Management

### Multi-Repository Support

```plantuml
@startuml
!theme plain

class GitHubServiceManager {
    - _services: Dictionary<string, IGitHubService>
    - _currentService: IGitHubService
    - _configuration: GitHubConfig
    --
    + InitializeServices(config): void
    + SwitchRepository(repoId): void
    + GetCurrentRepository(): RepositoryInfo
    + GetRepositories(): List<RepositoryInfo>
}

class GitHubRepositoryConfig {
    + Id: string
    + Owner: string
    + Repository: string
    + PersonalAccessToken: string
    + DisplayName: string
}

GitHubServiceManager --> GitHubService : manages multiple
GitHubServiceManager --> GitHubRepositoryConfig : configured by

@enduml
```

## Error Handling and Validation

### Input Validation Flow

```plantuml
@startuml
!theme plain

title Input Validation System

start

if (Input is Date?) then (yes)
    if (Matches YYYY-MM-DD?) then (yes)
        if (Date >= Today?) then (yes)
            :Accept;
        else (no)
            :Show "Date cannot be in past";
        endif
    else (no)
        :Show "Invalid date format";
    endif
elseif (Input is Estimate?) then (yes)
    if (Matches time pattern?) then (yes)
        :Parse duration;
        :Accept;
    else (no)
        :Show "Use format like 1d2h30m";
    endif
elseif (Input is Tag?) then (yes)
    if (Not empty?) then (yes)
        if (No special chars?) then (yes)
            :Accept;
        else (no)
            :Show "Invalid characters in tag";
        endif
    else (no)
        :Show "Tag cannot be empty";
    endif
else (other)
    :Validate based on type;
endif

stop

@enduml
```

## Loading and Performance

### Async Operation Pattern

```plantuml
@startuml
!theme plain

title Async Loading Pattern

participant "UI Action" as Action
participant "Loading Indicator" as Loading
participant "Async Operation" as Async
participant "UI Update" as Update
participant "Error Handler" as Error

Action -> Loading: ShowLoadingIndicator()
activate Loading
Loading -> Loading: Start rotation animation

Action -> Async: Begin async operation
activate Async

alt Success
    Async --> Action: Result data
    Action -> Update: Update UI with data
else Failure
    Async --> Action: Exception
    Action -> Error: DisplayAlert(error)
end

deactivate Async

Action -> Loading: HideLoadingIndicator()
Loading -> Loading: Cancel animation
deactivate Loading

@enduml
```

## Configuration System

### Settings and Configuration

```plantuml
@startuml
!theme plain

class Configuration {
    + GitHubRepositories: List<GitHubRepositoryConfig>
    + DefaultRepositoryId: string
    + PostCommandDelay: int
    + StateStaleTimeout: TimeSpan
    + MaxIssuesOpen: int
    + MaxIssuesClosed: int
}

class SettingsLoader {
    - _configPath: string
    --
    + LoadConfiguration(): Configuration
    + SaveConfiguration(config): void
    + ValidateConfig(config): bool
}

note right of Configuration
    Loaded from:
    - appsettings.json
    - settings.local.json (override)
end note

@enduml
```

## Critical Bug Fixes Documentation

### Fix 1: Duplicate Command Execution
**Location**: `TerminalPage.xaml.cs` line 2103
**Issue**: `UpdateEmbeddedStateAfterCommand` called twice
**Fix**: Removed duplicate call after `PostCommandAndUpdateState`

### Fix 2: Tag Parsing from Embedded State
**Location**: `IssueStateParser.cs` line 44-50
**Issue**: Commands in JSON `lastCommand` field parsed as real commands
**Fix**: Strip embedded state section before parsing commands

### Fix 3: Loading Animation UI Freeze
**Location**: `TerminalPage.xaml.cs` line 2500
**Issue**: Infinite loop in UI thread
**Fix**: Moved animation to background Task with UI marshaling

## Future Considerations

1. **State Conflict Resolution**: When multiple clients update simultaneously
2. **Offline Support**: Cache state locally for offline viewing
3. **State Migration**: Handle schema version upgrades
4. **Batch Operations**: Process multiple commands in single update
5. **Audit Trail**: Track all state changes with full history
6. **Performance Monitoring**: Add telemetry for API call patterns
7. **Undo/Redo System**: Allow reverting state changes
8. **Bulk Operations**: Select multiple issues for batch updates

---

*Generated: 2025-01-23*
*Version: 1.0.0*
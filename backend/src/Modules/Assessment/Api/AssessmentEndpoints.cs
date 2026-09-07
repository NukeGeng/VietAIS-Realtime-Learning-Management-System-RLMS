using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Assessment.Api;

public static class AssessmentEndpoints
{
    public static void MapAssessmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/question-bank", ListQuestionBankItemsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapGet("/api/assessment/question-bank/{itemId:guid}", GetQuestionBankItemAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/question-bank", CreateQuestionBankItemAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/question-bank/{itemId:guid}/versions",
                CreateQuestionVersionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);

        endpoints.MapGet("/api/assessment/classes/{classId:guid}/activities", ListActivitiesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/classes/{classId:guid}/activities", CreateActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapGet("/api/assessment/activities/{activityId:guid}", GetActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/activities/{activityId:guid}/overview",
                GetOverviewAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapGet("/api/assessment/attempts/{attemptId:guid}/detail", GetAttemptDetailAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/questions",
                AddActivityQuestionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/publish", PublishActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/activities/{activityId:guid}/publish",
                PublishActivityInSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/pause", PauseActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/resume", ResumeActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/close", CloseActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);

        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/attempt/start",
                StartAttemptAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapGet("/api/assessment/activities/{activityId:guid}/attempt",
                GetAttemptAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/attempt/questions/{questionId:guid}/open",
                OpenQuestionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPut("/api/assessment/activities/{activityId:guid}/attempt/answers/{questionId:guid}",
                SaveAnswerAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/attempt/submit",
                SubmitAttemptAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapGradingEndpoints();
    }

    private static async Task<IResult> ListQuestionBankItemsAsync(
        HttpContext context, IQuerySession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var items = await session.Query<QuestionBankItem>()
            .Where(item => item.Status == QuestionBankStatus.Active)
            .OrderBy(item => item.Topic)
            .ThenBy(item => item.UpdatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(QuestionBankItemResponse.From));
    }

    private static async Task<IResult> GetQuestionBankItemAsync(
        Guid itemId, IQuerySession session, CancellationToken cancellationToken)
    {
        var item = await session.LoadAsync<QuestionBankItem>(itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        var versions = await session.Query<QuestionVersion>()
            .Where(version => version.QuestionBankItemId == itemId)
            .OrderByDescending(version => version.Version)
            .ToListAsync(cancellationToken);
        return Results.Ok(QuestionBankItemDetailResponse.From(item, versions));
    }

    private static async Task<IResult> CreateQuestionBankItemAsync(
        CreateQuestionBankItemRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var topic = AssessmentRules.RequireTopic(request.Topic);
            var prompt = AssessmentRules.RequirePrompt(request.Prompt);
            var options = NormalizeOptions(request.Options);
            var correctAnswers = NormalizeAnswers(request.CorrectAnswers);
            AssessmentRules.ValidateQuestion(request.Type, options, correctAnswers);
            if (request.CourseId is { } courseId &&
                await session.LoadAsync<Course>(courseId, cancellationToken) is null)
            {
                return Results.BadRequest(new { error = "The selected course does not exist." });
            }

            var now = DateTimeOffset.UtcNow;
            var item = new QuestionBankItem
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                CourseId = request.CourseId,
                Topic = topic,
                Difficulty = AssessmentRules.NormalizeDifficulty(request.Difficulty),
                CurrentVersion = 1,
                Status = QuestionBankStatus.Active,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            var version = new QuestionVersion
            {
                Id = AssessmentConstants.QuestionVersionId(item.Id, 1),
                OrganizationId = actor.OrganizationId,
                QuestionBankItemId = item.Id,
                Version = 1,
                Prompt = prompt,
                Type = request.Type,
                Options = options,
                CorrectAnswers = correctAnswers,
                CreatedAt = now,
                CreatedBy = actor.UserId
            };
            session.Insert(item);
            session.Insert(version);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/question-bank/{item.Id:D}",
                QuestionBankItemDetailResponse.From(item, [version]));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> CreateQuestionVersionAsync(
        Guid itemId,
        CreateQuestionVersionRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var item = await session.LoadAsync<QuestionBankItem>(itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Status == QuestionBankStatus.Archived)
        {
            return Results.Conflict(new { error = "An archived question cannot receive a new version." });
        }

        try
        {
            var options = NormalizeOptions(request.Options);
            var correctAnswers = NormalizeAnswers(request.CorrectAnswers);
            AssessmentRules.ValidateQuestion(request.Type, options, correctAnswers);
            var versionNumber = item.CurrentVersion + 1;
            var now = DateTimeOffset.UtcNow;
            var version = new QuestionVersion
            {
                Id = AssessmentConstants.QuestionVersionId(item.Id, versionNumber),
                OrganizationId = actor!.OrganizationId,
                QuestionBankItemId = item.Id,
                Version = versionNumber,
                Prompt = AssessmentRules.RequirePrompt(request.Prompt),
                Type = request.Type,
                Options = options,
                CorrectAnswers = correctAnswers,
                CreatedAt = now,
                CreatedBy = actor.UserId
            };
            item.CurrentVersion = versionNumber;
            item.UpdatedAt = now;
            session.Insert(version);
            session.Update(item);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/question-bank/{item.Id:D}/versions/{version.Version}",
                QuestionVersionResponse.From(version));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> CreateActivityAsync(
        Guid classId,
        CreateActivityRequest request,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        try
        {
            var times = ValidateActivityTimes(request.OpensAt, request.DueAt, request.TimeLimit);
            var examPolicy = AssessmentRules.ValidateExamPolicy(request.Kind, request.ExamPolicy);
            var now = DateTimeOffset.UtcNow;
            var activity = new AssessmentActivity
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                Title = AssessmentRules.RequireTitle(request.Title),
                Description = AssessmentRules.RequireDescription(request.Description),
                Kind = AssessmentRules.RequireActivityKind(request.Kind),
                Status = AssessmentActivityStatus.Draft,
                OpensAt = times.OpensAt,
                DueAt = times.DueAt,
                TimeLimit = times.TimeLimit,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now,
                ExamPolicy = examPolicy
            };
            session.Insert(activity);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/activities/{activity.Id:D}",
                AssessmentActivityResponse.From(activity));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> ListActivitiesAsync(
        Guid classId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor, classId, academics, cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<AssessmentActivity>()
            .Where(activity => activity.LearningClassId == classId);
        if (!canManage)
        {
            query = query.Where(activity => activity.Status != AssessmentActivityStatus.Draft);
        }

        var activities = await query.OrderByDescending(activity => activity.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(activities.Select(AssessmentActivityResponse.From));
    }

    private static async Task<IResult> GetActivityAsync(
        Guid activityId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanAccessActivityAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        return Results.Ok(AssessmentActivityResponse.From(activity));
    }

    private static async Task<IResult> AddActivityQuestionAsync(
        Guid activityId,
        AddActivityQuestionRequest request,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (activity.Status != AssessmentActivityStatus.Draft)
        {
            return Results.Conflict(new { error = "Questions cannot be changed after an activity is published." });
        }

        if (activity.Questions.Count >= AssessmentRules.MaxQuestionsPerActivity)
        {
            return Results.BadRequest(new { error = "The activity has reached its question limit." });
        }

        var version = await session.LoadAsync<QuestionVersion>(request.QuestionVersionId, cancellationToken);
        if (version is null)
        {
            return Results.BadRequest(new { error = "The selected question version does not exist." });
        }

        var item = await session.LoadAsync<QuestionBankItem>(version.QuestionBankItemId, cancellationToken);
        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, activity.LearningClassId,
            cancellationToken);
        if (item is null || learningClass is null || item.CourseId is { } courseId && courseId != learningClass.CourseId)
        {
            return Results.BadRequest(new { error = "The question does not belong to this class's course." });
        }

        try
        {
            var order = request.Order < 0 ? throw new AssessmentValidationException("Question order cannot be negative.") : request.Order;
            var points = AssessmentRules.RequirePoints(request.Points);
            if (activity.Questions.Any(question => question.Order == order))
            {
                return Results.Conflict(new { error = "A question with that order already exists." });
            }

            if (activity.Questions.Any(question => question.QuestionVersionId == version.Id))
            {
                return Results.Conflict(new { error = "The question version is already part of this activity." });
            }

            var activityQuestion = new ActivityQuestion
            {
                Id = AssessmentConstants.ActivityQuestionId(activity.Id, version.Id),
                QuestionVersionId = version.Id,
                Order = order,
                Points = points,
                Prompt = version.Prompt,
                Type = version.Type,
                Options = version.Options,
                CorrectAnswers = version.CorrectAnswers
            };
            activity.Questions.Add(activityQuestion);
            activity.Questions = activity.Questions.OrderBy(question => question.Order).ToList();
            activity.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(activity);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(AssessmentActivityResponse.From(activity));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetOverviewAsync(
        Guid classId,
        Guid activityId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null || activity.LearningClassId != classId)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var enrollments = await session.Query<Enrollment>()
            .Where(enrollment => enrollment.LearningClassId == classId &&
                enrollment.Status == EnrollmentStatus.Active)
            .OrderBy(enrollment => enrollment.StudentId)
            .ToListAsync(cancellationToken);
        var view = await session.LoadAsync<ClassOverviewView>(
            AssessmentConstants.ClassOverviewViewId(classId, activityId), cancellationToken);
        var summaries = view?.Students.ToDictionary(student => student.StudentId) ??
            new Dictionary<Guid, ClassOverviewStudent>();
        var students = enrollments.Select(enrollment => summaries.TryGetValue(enrollment.StudentId,
                out var summary)
            ? ClassOverviewStudentResponse.From(summary)
            : ClassOverviewStudentResponse.NotStarted(enrollment.StudentId, activity.Questions.Count)).ToArray();
        return Results.Ok(new ClassOverviewResponse(classId, activityId, view?.Version ?? 0,
            view?.UpdatedAt, students));
    }

    private static async Task<IResult> GetAttemptDetailAsync(
        Guid attemptId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var detail = await session.LoadAsync<StudentAttemptDetailView>(attemptId, cancellationToken);
        if (detail is null)
        {
            var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
            if (attempt is null)
            {
                return Results.NotFound();
            }

            var activity = await session.LoadAsync<AssessmentActivity>(attempt.ActivityId, cancellationToken);
            if (activity is null)
            {
                return Results.NotFound();
            }

            detail = StudentAttemptDetailViewFrom(attempt, activity.Questions.Count);
        }

        if (detail.OrganizationId != actor!.OrganizationId ||
            !await CanManageClassAsync(actor, detail.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        return Results.Ok(StudentAttemptDetailResponse.From(detail));
    }

    private static async Task<IResult> PublishActivityAsync(
        Guid activityId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        return await PublishActivityCoreAsync(activityId, null, context, session, academics, cancellationToken);
    }

    private static async Task<IResult> PublishActivityInSessionAsync(
        Guid sessionId,
        Guid activityId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (liveSession.Status != LiveSessionStatus.Started)
        {
            return Results.Conflict(new { error = "An activity can only be published in a started session." });
        }

        return await PublishActivityCoreAsync(activityId, liveSession, context, session, academics,
            cancellationToken);
    }

    private static async Task<IResult> PublishActivityCoreAsync(
        Guid activityId,
        LiveSession? liveSession,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (liveSession is not null && liveSession.LearningClassId != activity.LearningClassId)
        {
            return Results.BadRequest(new { error = "The activity and live session belong to different classes." });
        }

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (activity.Status == AssessmentActivityStatus.Published)
        {
            if (liveSession is not null && activity.LiveSessionId != liveSession.Id)
            {
                return Results.Conflict(new { error = "A published activity cannot move to another live session." });
            }

            return Results.Ok(AssessmentActivityResponse.From(activity));
        }

        if (activity.Status is AssessmentActivityStatus.Paused or AssessmentActivityStatus.Closed)
        {
            return Results.Conflict(new { error = "This activity cannot be published in its current state." });
        }

        if (activity.Questions.Count == 0)
        {
            return Results.Conflict(new { error = "An activity must contain at least one question." });
        }

        activity.Status = AssessmentActivityStatus.Published;
        activity.LiveSessionId = liveSession?.Id;
        activity.PublishedAt = DateTimeOffset.UtcNow;
        activity.UpdatedAt = activity.PublishedAt.Value;
        await GradingWorkflow.EnsureGradeItemAsync(session, activity, activity.PublishedAt.Value,
            cancellationToken);
        session.Update(activity);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(AssessmentActivityResponse.From(activity));
    }

    private static Task<IResult> PauseActivityAsync(
        Guid activityId, HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        CancellationToken cancellationToken) =>
        ChangeActivityStatusAsync(activityId, AssessmentActivityStatus.Paused, context, session, academics,
            cancellationToken);

    private static Task<IResult> ResumeActivityAsync(
        Guid activityId, HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        CancellationToken cancellationToken) =>
        ChangeActivityStatusAsync(activityId, AssessmentActivityStatus.Published, context, session, academics,
            cancellationToken);

    private static Task<IResult> CloseActivityAsync(
        Guid activityId, HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        CancellationToken cancellationToken) =>
        ChangeActivityStatusAsync(activityId, AssessmentActivityStatus.Closed, context, session, academics,
            cancellationToken);

    private static async Task<IResult> ChangeActivityStatusAsync(
        Guid activityId,
        AssessmentActivityStatus target,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var valid = (target, activity.Status) switch
        {
            (AssessmentActivityStatus.Paused, AssessmentActivityStatus.Published) => true,
            (AssessmentActivityStatus.Published, AssessmentActivityStatus.Paused) => true,
            (AssessmentActivityStatus.Closed, AssessmentActivityStatus.Published or AssessmentActivityStatus.Paused) => true,
            _ => false
        };
        if (!valid)
        {
            return Results.Conflict(new { error = "The activity cannot make that state transition." });
        }

        activity.Status = target;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(activity);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(AssessmentActivityResponse.From(activity));
    }

    private static async Task<IResult> StartAttemptAsync(
        Guid activityId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!IsOpen(activity, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "The activity is not currently open." });
        }

        var latestAttempt = await FindLatestAttemptAsync(session, activity.Id, actor!.UserId,
            cancellationToken);
        var attemptNumber = latestAttempt?.AttemptNumber ?? 0;
        if (latestAttempt is not null && latestAttempt.Status is StudentActivityStatus.InProgress or
            StudentActivityStatus.Paused)
        {
            return Results.Ok(StudentActivityResponse.From(latestAttempt));
        }

        if (latestAttempt is not null && (!IsExamRetakeAllowed(activity, attemptNumber)))
        {
            return Results.Conflict(new { error = "This activity attempt is already finished." });
        }

        attemptNumber = Math.Max(attemptNumber + 1, 1);
        var attemptId = AssessmentConstants.StudentActivityId(activity.Id, actor!.UserId,
            attemptNumber);
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is not null)
        {
            return Results.Ok(StudentActivityResponse.From(stream.Aggregate));
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var startedEvent = new StudentActivityStarted(attemptId, actor.OrganizationId, actor.UserId,
            activity.Id, activity.LearningClassId, activity.LiveSessionId, now, attemptNumber);
        var nextState = new StudentActivity();
        nextState.Apply(startedEvent);
        stream.AppendOne(startedEvent);
        await session.SaveChangesAsync(cancellationToken);
        var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId, ActivityChangeTypes.Started, null, null,
                StudentActivityRealtimeState.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(attempt!));
    }

    private static async Task<IResult> GetAttemptAsync(
        Guid activityId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var attempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        return attempt is null ? Results.NotFound() : Results.Ok(StudentActivityResponse.From(attempt));
    }

    private static async Task<IResult> OpenQuestionAsync(
        Guid activityId,
        Guid questionId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (activity.Questions.All(question => question.Id != questionId))
        {
            return Results.NotFound();
        }

        if (!IsOpen(activity, DateTimeOffset.UtcNow) || activity.Status != AssessmentActivityStatus.Published)
        {
            return Results.Conflict(new { error = "The activity is not currently open." });
        }

        var currentAttempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        if (currentAttempt is null)
        {
            return Results.Conflict(new { error = "Start the activity before opening a question." });
        }

        var attemptId = currentAttempt.Id;
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is null)
        {
            return Results.Conflict(new { error = "Start the activity before opening a question." });
        }

        if (stream.Aggregate.Status != StudentActivityStatus.InProgress ||
            !IsAttemptOpen(activity, stream.Aggregate, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "This activity attempt is not accepting interaction." });
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var openedEvent = new QuestionOpened(attemptId, questionId, now);
        var nextState = Clone(stream.Aggregate);
        nextState.Apply(openedEvent);
        stream.AppendOne(openedEvent);
        await session.SaveChangesAsync(cancellationToken);
        var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId, ActivityChangeTypes.QuestionOpened, questionId, null,
                StudentActivityRealtimeState.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(attempt!));
    }

    private static async Task<IResult> SaveAnswerAsync(
        Guid activityId,
        Guid questionId,
        SaveAnswerRequest request,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (activity.Status != AssessmentActivityStatus.Published || !IsOpen(activity, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "The activity is not currently accepting answers." });
        }

        var question = activity.Questions.SingleOrDefault(candidate => candidate.Id == questionId);
        if (question is null)
        {
            return Results.NotFound();
        }

        string answer;
        try
        {
            answer = NormalizeAnswer(question, AssessmentRules.RequireAnswer(request.Value));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }

        var currentAttempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        if (currentAttempt is null)
        {
            return Results.Conflict(new { error = "Start the activity before saving an answer." });
        }

        var attemptId = currentAttempt.Id;
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is null)
        {
            return Results.Conflict(new { error = "Start the activity before saving an answer." });
        }

        if (stream.Aggregate.Status != StudentActivityStatus.InProgress ||
            !IsAttemptOpen(activity, stream.Aggregate, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "This activity attempt is not accepting answers." });
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var changed = stream.Aggregate.Answers.ContainsKey(questionId);
        var answerEvent = changed
            ? (object)new AnswerChanged(attemptId, questionId, answer, now)
            : new AnswerSubmitted(attemptId, questionId, answer, now);
        var nextState = Clone(stream.Aggregate);
        if (answerEvent is AnswerChanged changedEvent)
        {
            nextState.Apply(changedEvent);
        }
        else
        {
            nextState.Apply((AnswerSubmitted)answerEvent);
        }
        stream.AppendOne(answerEvent);
        await session.SaveChangesAsync(cancellationToken);
        var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId,
                changed ? ActivityChangeTypes.AnswerChanged : ActivityChangeTypes.AnswerSubmitted,
                questionId, answer, StudentActivityRealtimeState.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(attempt!));
    }

    private static async Task<IResult> SubmitAttemptAsync(
        Guid activityId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (activity.Status != AssessmentActivityStatus.Published || !IsOpen(activity, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "The activity is not currently accepting submissions." });
        }

        var currentAttempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        if (currentAttempt is null)
        {
            return Results.Conflict(new { error = "Start the activity before submitting it." });
        }

        var attemptId = currentAttempt.Id;
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is null)
        {
            return Results.Conflict(new { error = "Start the activity before submitting it." });
        }

        if (stream.Aggregate.Status == StudentActivityStatus.Submitted)
        {
            var existing = await session.LoadAsync<Submission>(AssessmentConstants.SubmissionId(
                activityId, actor.UserId, stream.Aggregate.AttemptNumber), cancellationToken);
            return existing is null ? Results.Conflict(new { error = "The activity is already submitted." }) :
                Results.Ok(SubmissionResponse.From(existing));
        }

        if (stream.Aggregate.Status != StudentActivityStatus.InProgress ||
            !IsAttemptOpen(activity, stream.Aggregate, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "This activity attempt cannot be submitted." });
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var submittedEvent = new ActivitySubmitted(attemptId, now);
        var nextState = Clone(stream.Aggregate);
        nextState.Apply(submittedEvent);
        stream.AppendOne(submittedEvent);
        var submission = new Submission
        {
            Id = AssessmentConstants.SubmissionId(activityId, actor.UserId,
                stream.Aggregate.AttemptNumber),
            OrganizationId = actor.OrganizationId,
            ActivityId = activityId,
            StudentId = actor.UserId,
            StudentActivityId = attemptId,
            AttemptNumber = stream.Aggregate.AttemptNumber,
            SubmittedAt = now,
            Status = SubmissionStatus.Submitted,
            Version = checked((int)(stream.Aggregate.Version + 1)),
            Answers = stream.Aggregate.Answers
                .Select(answer => new SubmissionAnswer(answer.Key, answer.Value))
                .ToArray()
        };
        session.Insert(submission);
        var gradeItem = await GradingWorkflow.EnsureGradeItemAsync(session, activity, now,
            cancellationToken);
        var automaticGrade = GradingRules.CalculateAutomaticGrade(activity, submission);
        var gradeEntry = GradingWorkflow.CreateGradeEntry(activity, submission, gradeItem,
            automaticGrade, now);
        session.Insert(gradeEntry);
        await session.SaveChangesAsync(cancellationToken);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId, ActivityChangeTypes.Submitted, null, null,
                StudentActivityRealtimeState.From(nextState), now));
        return Results.Created($"/api/assessment/activities/{activityId:D}/submissions/{submission.Id:D}",
            SubmissionResponse.From(submission));
    }

    private static async Task<bool> CanManageClassAsync(
        AuthenticatedActor actor, Guid classId, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        return actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
    }

    private static async Task<bool> CanAccessActivityAsync(
        AuthenticatedActor actor, AssessmentActivity activity, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (actor.Permissions.Contains(PermissionCatalog.ManageAssessmentActivities, StringComparer.Ordinal))
        {
            return await CanManageClassAsync(actor, activity.LearningClassId, academics, cancellationToken);
        }

        return activity.Status != AssessmentActivityStatus.Draft &&
            await CanStudentAccessAsync(actor, activity, academics, cancellationToken);
    }

    private static async Task<bool> CanStudentAccessAsync(
        AuthenticatedActor actor, AssessmentActivity activity, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (activity.OrganizationId != actor.OrganizationId ||
            !actor.Permissions.Contains(PermissionCatalog.SubmitAssessmentActivities, StringComparer.Ordinal))
        {
            return false;
        }

        if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, activity.LearningClassId,
                actor.UserId, cancellationToken))
        {
            return false;
        }

        // A live activity remains readable after the session ends; its open/due rules still govern mutation.
        return true;
    }

    private static bool IsOpen(AssessmentActivity activity, DateTimeOffset now)
    {
        return activity.Status == AssessmentActivityStatus.Published &&
            (activity.OpensAt is null || now >= activity.OpensAt) &&
            (activity.DueAt is null || now <= activity.DueAt);
    }

    private static bool IsAttemptOpen(AssessmentActivity activity, StudentActivity attempt,
        DateTimeOffset now)
    {
        return IsOpen(activity, now) &&
            (activity.TimeLimit is not { } limit || now <= attempt.StartedAt + limit);
    }

    private static async Task<StudentActivity?> FindLatestAttemptAsync(IQuerySession session,
        Guid activityId, Guid studentId, CancellationToken cancellationToken)
    {
        var attempts = await session.Query<StudentActivity>()
            .Where(attempt => attempt.ActivityId == activityId && attempt.StudentId == studentId)
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .ToListAsync(cancellationToken);
        return attempts.FirstOrDefault();
    }

    private static bool IsExamRetakeAllowed(AssessmentActivity activity, int attemptNumber)
    {
        return activity.Kind == AssessmentActivityKind.Exam && activity.ExamPolicy is
        { AllowRetake: true } policy && attemptNumber < policy.AttemptLimit;
    }

    private static StudentActivity Clone(StudentActivity source) => new()
    {
        Id = source.Id,
        OrganizationId = source.OrganizationId,
        StudentId = source.StudentId,
        ActivityId = source.ActivityId,
        LearningClassId = source.LearningClassId,
        LiveSessionId = source.LiveSessionId,
        CurrentQuestionId = source.CurrentQuestionId,
        Status = source.Status,
        Answers = new Dictionary<Guid, string>(source.Answers),
        AnsweredCount = source.AnsweredCount,
        StartedAt = source.StartedAt,
        LastActivityAt = source.LastActivityAt,
        SubmittedAt = source.SubmittedAt,
        Version = source.Version,
        AttemptNumber = source.AttemptNumber
    };

    private static StudentAttemptDetailView StudentAttemptDetailViewFrom(StudentActivity activity,
        int totalQuestions) => new()
        {
            Id = activity.Id,
            OrganizationId = activity.OrganizationId,
            StudentId = activity.StudentId,
            AttemptNumber = activity.AttemptNumber,
            ActivityId = activity.ActivityId,
            LearningClassId = activity.LearningClassId,
            LiveSessionId = activity.LiveSessionId,
            CurrentQuestionId = activity.CurrentQuestionId,
            Status = activity.Status,
            Answers = new Dictionary<Guid, string>(activity.Answers),
            AnsweredCount = activity.AnsweredCount,
            TotalQuestions = totalQuestions,
            Progress = totalQuestions == 0 ? 0m : decimal.Round(activity.AnsweredCount * 100m / totalQuestions, 2),
            StartedAt = activity.StartedAt,
            LastActivityAt = activity.LastActivityAt,
            SubmittedAt = activity.SubmittedAt,
            Version = activity.Version
        };

    private static (DateTimeOffset? OpensAt, DateTimeOffset? DueAt, TimeSpan? TimeLimit) ValidateActivityTimes(
        DateTimeOffset? opensAt, DateTimeOffset? dueAt, TimeSpan? timeLimit)
    {
        if (opensAt is not null && dueAt is not null && dueAt <= opensAt)
        {
            throw new AssessmentValidationException("The activity due time must be after its opening time.");
        }

        if (timeLimit is { } limit && (limit <= TimeSpan.Zero || limit > TimeSpan.FromDays(30)))
        {
            throw new AssessmentValidationException("The activity time limit must be positive and at most 30 days.");
        }

        return (opensAt, dueAt, timeLimit);
    }

    private static QuestionOption[] NormalizeOptions(QuestionOptionRequest[]? options)
    {
        return (options ?? [])
            .Select(option => new QuestionOption(option.Key.Trim(), option.Text.Trim()))
            .ToArray();
    }

    private static string[] NormalizeAnswers(string[]? answers) =>
        (answers ?? []).Select(answer => answer.Trim()).Where(answer => answer.Length > 0).ToArray();

    private static string NormalizeAnswer(ActivityQuestion question, string value)
    {
        if (question.Type == QuestionType.TrueFalse)
        {
            if (!new[] { "true", "false" }.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                throw new AssessmentValidationException("The answer must be true or false.");
            }

            return value.ToLowerInvariant();
        }

        if (question.Type == QuestionType.SingleChoice)
        {
            var option = question.Options.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, value, StringComparison.OrdinalIgnoreCase));
            if (option is null)
            {
                throw new AssessmentValidationException("The answer is not one of the available options.");
            }

            return option.Key;
        }

        if (question.Type == QuestionType.MultipleChoice)
        {
            var values = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 0)
            {
                throw new AssessmentValidationException("At least one option must be selected.");
            }

            var keys = values.Select(selected => question.Options.FirstOrDefault(option =>
                    string.Equals(option.Key, selected, StringComparison.OrdinalIgnoreCase))?.Key)
                .ToArray();
            if (keys.Any(key => key is null) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
            {
                throw new AssessmentValidationException("The answer contains an unavailable or repeated option.");
            }

            return string.Join(',', keys.Order(StringComparer.Ordinal));
        }

        return value;
    }
}

public sealed record CreateQuestionBankItemRequest(Guid? CourseId, string Topic, string? Difficulty,
    string Prompt, QuestionType Type, QuestionOptionRequest[]? Options, string[]? CorrectAnswers);

public sealed record CreateQuestionVersionRequest(string Prompt, QuestionType Type,
    QuestionOptionRequest[]? Options, string[]? CorrectAnswers);

public sealed record QuestionOptionRequest(string Key, string Text);

public sealed record CreateActivityRequest(string Title, string? Description, AssessmentActivityKind Kind,
    DateTimeOffset? OpensAt, DateTimeOffset? DueAt, TimeSpan? TimeLimit, ExamPolicy? ExamPolicy = null);

public sealed record AddActivityQuestionRequest(Guid QuestionVersionId, int Order, decimal Points);

public sealed record SaveAnswerRequest(string Value);

public sealed record QuestionBankItemResponse(Guid Id, Guid? CourseId, string Topic, string? Difficulty,
    int CurrentVersion, QuestionBankStatus Status)
{
    public static QuestionBankItemResponse From(QuestionBankItem item) => new(item.Id, item.CourseId,
        item.Topic, item.Difficulty, item.CurrentVersion, item.Status);
}

public sealed record QuestionVersionResponse(Guid Id, Guid QuestionBankItemId, int Version,
    string Prompt, QuestionType Type, QuestionOption[] Options, string[] CorrectAnswers)
{
    public static QuestionVersionResponse From(QuestionVersion version) => new(version.Id,
        version.QuestionBankItemId, version.Version, version.Prompt, version.Type, version.Options,
        version.CorrectAnswers);
}

public sealed record QuestionBankItemDetailResponse(QuestionBankItemResponse Item,
    QuestionVersionResponse[] Versions)
{
    public static QuestionBankItemDetailResponse From(QuestionBankItem item,
        IEnumerable<QuestionVersion> versions) => new(QuestionBankItemResponse.From(item),
        versions.Select(QuestionVersionResponse.From).ToArray());
}

public sealed record ActivityQuestionResponse(Guid Id, Guid QuestionVersionId, int Order, decimal Points,
    string Prompt, QuestionType Type, QuestionOption[] Options)
{
    public static ActivityQuestionResponse From(ActivityQuestion question) => new(question.Id,
        question.QuestionVersionId, question.Order, question.Points, question.Prompt, question.Type,
        question.Options);
}

public sealed record AssessmentActivityResponse(Guid Id, Guid LearningClassId, Guid? LiveSessionId,
    string Title, string Description, AssessmentActivityKind Kind, AssessmentActivityStatus Status,
    DateTimeOffset? OpensAt, DateTimeOffset? DueAt, TimeSpan? TimeLimit, DateTimeOffset? PublishedAt,
    ActivityQuestionResponse[] Questions, ExamPolicy? ExamPolicy)
{
    public static AssessmentActivityResponse From(AssessmentActivity activity) => new(activity.Id,
        activity.LearningClassId, activity.LiveSessionId, activity.Title, activity.Description, activity.Kind,
        activity.Status, activity.OpensAt, activity.DueAt, activity.TimeLimit, activity.PublishedAt,
        activity.Questions.OrderBy(question => question.Order).Select(ActivityQuestionResponse.From).ToArray(),
        activity.ExamPolicy);
}

public sealed record StudentActivityResponse(Guid Id, Guid StudentId, Guid ActivityId,
    Guid LearningClassId, Guid? LiveSessionId, Guid? CurrentQuestionId, StudentActivityStatus Status,
    Dictionary<Guid, string> Answers, int AnsweredCount, DateTimeOffset StartedAt,
    DateTimeOffset LastActivityAt, DateTimeOffset? SubmittedAt, long Version, int AttemptNumber)
{
    public static StudentActivityResponse From(StudentActivity activity) => new(activity.Id,
        activity.StudentId, activity.ActivityId, activity.LearningClassId, activity.LiveSessionId,
        activity.CurrentQuestionId, activity.Status, new Dictionary<Guid, string>(activity.Answers),
        activity.AnsweredCount, activity.StartedAt, activity.LastActivityAt, activity.SubmittedAt,
        activity.Version, activity.AttemptNumber);
}

public sealed record SubmissionResponse(Guid Id, Guid ActivityId, Guid StudentId,
    Guid StudentActivityId, int AttemptNumber, DateTimeOffset SubmittedAt, SubmissionStatus Status,
    int Version, SubmissionAnswer[] Answers)
{
    public static SubmissionResponse From(Submission submission) => new(submission.Id, submission.ActivityId,
        submission.StudentId, submission.StudentActivityId, submission.AttemptNumber,
        submission.SubmittedAt, submission.Status, submission.Version, submission.Answers);
}

public sealed record ClassOverviewResponse(Guid LearningClassId, Guid ActivityId, long Version,
    DateTimeOffset? UpdatedAt, ClassOverviewStudentResponse[] Students);

public sealed record ClassOverviewStudentResponse(Guid StudentId, Guid AttemptId,
    Guid? CurrentQuestionId, int AnsweredCount, int TotalQuestions, decimal Progress,
    StudentActivityStatus? Status, DateTimeOffset? LastActivityAt, bool HelpRequested, long Version)
{
    public static ClassOverviewStudentResponse From(ClassOverviewStudent student) => new(student.StudentId,
        student.AttemptId, student.CurrentQuestionId, student.AnsweredCount, student.TotalQuestions,
        student.Progress, student.Status, student.LastActivityAt, student.HelpRequested, student.Version);

    public static ClassOverviewStudentResponse NotStarted(Guid studentId, int totalQuestions) =>
        new(studentId, Guid.Empty, null, 0, totalQuestions, 0m, null, null, false, 0);
}

public sealed record StudentAttemptDetailResponse(Guid Id, Guid OrganizationId, Guid StudentId,
    int AttemptNumber, Guid ActivityId, Guid LearningClassId, Guid? LiveSessionId, Guid? CurrentQuestionId,
    StudentActivityStatus Status, Dictionary<Guid, string> Answers, int AnsweredCount,
    int TotalQuestions, decimal Progress, DateTimeOffset StartedAt, DateTimeOffset LastActivityAt,
    DateTimeOffset? SubmittedAt, long Version)
{
    public static StudentAttemptDetailResponse From(StudentAttemptDetailView detail) => new(detail.Id,
        detail.OrganizationId, detail.StudentId, detail.AttemptNumber, detail.ActivityId,
        detail.LearningClassId,
        detail.LiveSessionId, detail.CurrentQuestionId, detail.Status,
        new Dictionary<Guid, string>(detail.Answers), detail.AnsweredCount, detail.TotalQuestions,
        detail.Progress, detail.StartedAt, detail.LastActivityAt, detail.SubmittedAt, detail.Version);
}

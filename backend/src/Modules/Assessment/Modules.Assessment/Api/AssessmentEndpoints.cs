using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
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
        LearningPlatform.Assessment.Features.Assessment.ListQuestionBankItems.ListQuestionBankItemsEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.GetQuestionBankItem.GetQuestionBankItemEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.CreateQuestionBankItem.CreateQuestionBankItemEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.CreateQuestionVersion.CreateQuestionVersionEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.ListActivities.ListActivitiesEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.CreateActivity.CreateActivityEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.GetActivity.GetActivityEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.GetOverview.GetOverviewEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.ListHelpRequests.ListHelpRequestsEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.GetAttemptDetail.GetAttemptDetailEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.AddActivityQuestion.AddActivityQuestionEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.PublishActivity.PublishActivityEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.PublishActivityInSession.PublishActivityInSessionEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.PauseActivity.PauseActivityEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.ResumeActivity.ResumeActivityEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.CloseActivity.CloseActivityEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.StartAttempt.StartAttemptEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.GetAttempt.GetAttemptEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.OpenQuestion.OpenQuestionEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.SaveAnswer.SaveAnswerEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.SubmitAttempt.SubmitAttemptEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.MarkAttemptIdle.MarkAttemptIdleEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.ResumeAttempt.ResumeAttemptEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.RequestAttemptHelp.RequestAttemptHelpEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.WithdrawAttemptHelp.WithdrawAttemptHelpEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.AcknowledgeAttemptHelp.AcknowledgeAttemptHelpEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Assessment.ResolveAttemptHelp.ResolveAttemptHelpEndpoint.Map(endpoints);
        endpoints.MapGradingEndpoints();
        endpoints.MapEssayEndpoints();
    }

}

using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Storage;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Assessment.Api;

public sealed record EssayGroupResponse(Guid Id, Guid EssayAssignmentId, Guid LearningClassId,
    string Name, EssayGroupStatus Status, EssayGroupMember[] Members)
{
    public static EssayGroupResponse From(EssayGroup group) => new(group.Id, group.EssayAssignmentId,
        group.LearningClassId, group.Name, group.Status, group.Members.ToArray());
}

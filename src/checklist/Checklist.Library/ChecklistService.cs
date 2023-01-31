/********************************************************************************
 * Copyright (c) 2021, 2023 Microsoft and BMW Group AG
 * Copyright (c) 2021, 2023 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Microsoft.Extensions.Logging;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.BusinessLogic;
using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;

public class ChecklistService : IChecklistService
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IBpdmBusinessLogic _bpdmBusinessLogic;
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;
    private readonly IClearinghouseBusinessLogic _clearinghouseBusinessLogic;
    private readonly ISdFactoryBusinessLogic _sdFactoryBusinessLogic;
    private readonly ILogger<IChecklistService> _logger;

    public ChecklistService(
        IPortalRepositories portalRepositories,
        IBpdmBusinessLogic bpdmBusinessLogic,
        ICustodianBusinessLogic custodianBusinessLogic,
        IClearinghouseBusinessLogic clearinghouseBusinessLogic,
        ISdFactoryBusinessLogic sdFactoryBusinessLogic,
        ILogger<IChecklistService> logger)
    {
        _portalRepositories = portalRepositories;
        _bpdmBusinessLogic = bpdmBusinessLogic;
        _custodianBusinessLogic = custodianBusinessLogic;
        _clearinghouseBusinessLogic = clearinghouseBusinessLogic;
        _sdFactoryBusinessLogic = sdFactoryBusinessLogic;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task TriggerBpnDataPush(Guid applicationId, string iamUserId, CancellationToken cancellationToken)
    {
        var checklistRepository = _portalRepositories.GetInstance<IApplicationChecklistRepository>();
        var checklistData = await checklistRepository.GetChecklistProcessStepData(applicationId, new [] { ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH, ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL }).ConfigureAwait(false);
        if (!checklistData.IsValidApplicationId)
        {
            throw new NotFoundException($"application {applicationId} does not exist");
        }
        if (!checklistData.IsSubmitted)
        {
            throw new ConflictException($"application {applicationId} is not in status SUBMITTED");
        }
        if (checklistData.Checklist == null || checklistData.ProcessSteps == null)
        {
            throw new UnexpectedConditionException("checklist or processSteps should never be null here");
        }
        if (!checklistData.Checklist.Any(entry => entry.TypeId == ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER && entry.StatusId == ApplicationChecklistEntryStatusId.TO_DO))
        {
            throw new ConflictException($"application {applicationId} does not have a checklist entry for {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER} in status {ApplicationChecklistEntryStatusId.TO_DO}");
        }
        var processStep = checklistData.ProcessSteps.SingleOrDefault(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH && step.ProcessStepStatusId == ProcessStepStatusId.TODO);
        if (processStep is null)
        {
            throw new ConflictException($"application {applicationId} checklist entry {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER}, process step {ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH} is not eligible to run");
        }
        if (checklistData.ProcessSteps.Any(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL && step.ProcessStepStatusId == ProcessStepStatusId.TODO))
        {
            throw new ConflictException($"application {applicationId} checklist entry {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER}, process step {ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL} is already scheduled to run");
        }

        await _bpdmBusinessLogic.TriggerBpnDataPush(applicationId, iamUserId, cancellationToken).ConfigureAwait(false);
        
        checklistRepository
            .AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, checklist =>
            {
                checklist.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.IN_PROGRESS;
            });
        _portalRepositories.GetInstance<IProcessStepRepository>().AttachAndModifyProcessStep(processStep.Id, null, step => step.ProcessStepStatusId = ProcessStepStatusId.DONE);
        ScheduleProcessSteps(this, applicationId, checklistData.ProcessSteps, ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL);
    }

    public async Task ProcessEndClearinghouse(Guid applicationId, CancellationToken cancellationToken)
    {
        var checklistRepository = _portalRepositories.GetInstance<IApplicationChecklistRepository>();
        var checklistData = await checklistRepository.GetChecklistProcessStepData(applicationId, new [] { ProcessStepTypeId.END_CLEARING_HOUSE, ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP }).ConfigureAwait(false);
        if (!checklistData.IsValidApplicationId)
        {
            throw new NotFoundException($"application {applicationId} does not exist");
        }
        if (!checklistData.IsSubmitted)
        {
            throw new ConflictException($"application {applicationId} is not in status SUBMITTED");
        }
        if (checklistData.Checklist == null || checklistData.ProcessSteps == null)
        {
            throw new UnexpectedConditionException("checklist or processSteps should never be null here");
        }
        if (!checklistData.Checklist.Any(entry => entry.TypeId == ApplicationChecklistEntryTypeId.CLEARING_HOUSE && entry.StatusId == ApplicationChecklistEntryStatusId.IN_PROGRESS))
        {
            throw new ConflictException($"application {applicationId} does not have a checklist entry for {ApplicationChecklistEntryTypeId.CLEARING_HOUSE} in status {ApplicationChecklistEntryStatusId.IN_PROGRESS}");
        }
        var processStep = checklistData.ProcessSteps.SingleOrDefault(step => step.ProcessStepTypeId == ProcessStepTypeId.END_CLEARING_HOUSE && step.ProcessStepStatusId == ProcessStepStatusId.TODO);
        if (processStep is null)
        {
            throw new ConflictException($"application {applicationId} checklist entry {ApplicationChecklistEntryTypeId.CLEARING_HOUSE}, process step {ProcessStepTypeId.END_CLEARING_HOUSE} is not eligible to run");
        }
        if (checklistData.ProcessSteps.Any(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP && step.ProcessStepStatusId == ProcessStepStatusId.TODO))
        {
            throw new ConflictException($"application {applicationId} checklist entry {ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP}, process step {ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP} is already scheduled to run");
        }

        // TODO implement call to businessLogic end clearinghouse 

        checklistRepository
            .AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.CLEARING_HOUSE, checklist =>
            {
                checklist.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE;
            });
        _portalRepositories.GetInstance<IProcessStepRepository>().AttachAndModifyProcessStep(processStep.Id, null, step => step.ProcessStepStatusId = ProcessStepStatusId.DONE);
        ScheduleProcessSteps(this, applicationId, checklistData.ProcessSteps, ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP);
    }

    private static readonly ImmutableDictionary<ProcessStepTypeId, (ApplicationChecklistEntryTypeId EntryTypeId, Func<ChecklistService,Guid,ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId>,IEnumerable<ProcessStep>,CancellationToken,Task<(ApplicationChecklistEntryStatusId ApplicationChecklistEntryStatusId, IEnumerable<ProcessStep> NextSteps, bool Modified)>> ProcessFunc)> _stepExecutions
        = new (ApplicationChecklistEntryTypeId ApplicationChecklistEntryTypeId, ProcessStepTypeId ProcessStepTypeId, Func<ChecklistService,Guid,ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId>,IEnumerable<ProcessStep>,CancellationToken,Task<(ApplicationChecklistEntryStatusId,IEnumerable<ProcessStep>,bool)>> ProcessFunc) []
        {
            new (ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, HandleBpnPull),
            new (ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ProcessStepTypeId.CREATE_IDENTITY_WALLET, CreateWalletAsync),
            new (ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ProcessStepTypeId.START_CLEARING_HOUSE, HandleClearingHouse),
            new (ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP, ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, HandleSelfDescription),
        }.ToImmutableDictionary(x => x.ProcessStepTypeId, x => (x.ApplicationChecklistEntryTypeId, x.ProcessFunc));

    private static readonly IEnumerable<ProcessStepTypeId> _manuelProcessSteps = new [] {
        ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH,
        ProcessStepTypeId.END_CLEARING_HOUSE,
    };

    /// <inheritdoc />
    public async IAsyncEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId, bool Processed)> ProcessChecklist(Guid applicationId, IEnumerable<(ApplicationChecklistEntryTypeId EntryTypeId, ApplicationChecklistEntryStatusId EntryStatusId)> checklist, IEnumerable<ProcessStep> processSteps, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var todoChecklist = checklist.ToDictionary(entry => entry.EntryTypeId, entry => entry.EntryStatusId);
        var todoSteps = new Queue<ProcessStep>(processSteps);
        var checklistRepository = _portalRepositories.GetInstance<IApplicationChecklistRepository>();
        var processStepRepository = _portalRepositories.GetInstance<IProcessStepRepository>();
        _logger.LogInformation("Found {StepsCount} possible steps for application {ApplicationId}", todoSteps.Count(), applicationId);
        while (todoSteps.TryDequeue(out var step))
        {
            if (_stepExecutions.TryGetValue(step.ProcessStepTypeId, out var execution))
            {
                if (!todoChecklist.TryGetValue(execution.EntryTypeId, out var returnStatusId))
                {
                    throw new ConflictException($"no checklist entry {execution.EntryTypeId} for {step.ProcessStepTypeId}");
                }
                var modified = false;
                try
                {
                    var result = await execution.ProcessFunc(this, applicationId, todoChecklist.ToImmutableDictionary(), todoSteps, cancellationToken).ConfigureAwait(false);
                    if (result.Modified)
                    {
                        foreach (var nextStep in result.NextSteps)
                        {
                            todoSteps.Enqueue(nextStep);
                        }
                        processStepRepository.AttachAndModifyProcessStep(step.Id, null, step => step.ProcessStepStatusId = ProcessStepStatusId.DONE);
                        returnStatusId = result.ApplicationChecklistEntryStatusId;
                        todoChecklist[execution.EntryTypeId] = returnStatusId;
                        modified = true;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (ex is not ServiceException { StatusCode: HttpStatusCode.ServiceUnavailable })
                    {
                        returnStatusId = ApplicationChecklistEntryStatusId.FAILED;
                    }

                    checklistRepository.AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.IDENTITY_WALLET,
                            item => { 
                                item.ApplicationChecklistEntryStatusId = returnStatusId;
                                item.Comment = ex.ToString(); 
                            });
                    modified = true;
                }
                yield return (execution.EntryTypeId, returnStatusId, modified);
            }
            else
            {
                throw new ConflictException($"no execution defined for processStep {step.ProcessStepTypeId}");
            }
        }
    }

    private static IEnumerable<ProcessStep> ScheduleProcessSteps(ChecklistService service, Guid applicationId, IEnumerable<ProcessStep> processSteps, params ProcessStepTypeId[] processStepTypeIds)
    {
        foreach (var processStepTypeId in processStepTypeIds)
        {
            if (!processSteps.Any(step => step.ProcessStepTypeId == processStepTypeId))
            {
                var step = service._portalRepositories.GetInstance<IProcessStepRepository>().CreateProcessStep(processStepTypeId, ProcessStepStatusId.TODO);
                service._portalRepositories.GetInstance<IApplicationChecklistRepository>().CreateApplicationAssignedProcessStep(applicationId, step.Id);
                yield return step;
            }
        }
    }

    private static async Task<(ApplicationChecklistEntryStatusId,IEnumerable<ProcessStep>,bool)> HandleBpnPull(ChecklistService service, Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        var businessPartnerNumber = string.Empty; // TODO add bpdm get legal entity call returning businessPartnerNumber
        if (string.IsNullOrWhiteSpace(businessPartnerNumber))
        {
            return (checklist[ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER], Enumerable.Empty<ProcessStep>(), false);
        }
        service._portalRepositories.GetInstance<IApplicationChecklistRepository>()
            .AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER,
                checklist =>
                {
                    checklist.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE;
                });
        service._portalRepositories.GetInstance<IProcessStepRepository>()
            .AttachAndModifyProcessStep(processSteps.Single(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL).Id, null, processStep => processStep.ProcessStepStatusId = ProcessStepStatusId.DONE);

        // TODO implement Handle Registration Verification - CREATE_IDENTITY_WALLET will not be triggered otherwise:
        var nextSteps = checklist[ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION] == ApplicationChecklistEntryStatusId.DONE
            ? ScheduleProcessSteps(service, applicationId, processSteps, ProcessStepTypeId.CREATE_IDENTITY_WALLET)
            : Enumerable.Empty<ProcessStep>();
        return (ApplicationChecklistEntryStatusId.DONE, nextSteps, true);
    }

    private static async Task<(ApplicationChecklistEntryStatusId,IEnumerable<ProcessStep>,bool)> CreateWalletAsync(ChecklistService service, Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        var message = await service._custodianBusinessLogic.CreateWalletAsync(applicationId, cancellationToken).ConfigureAwait(false);
        service._portalRepositories.GetInstance<IApplicationChecklistRepository>()
            .AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.IDENTITY_WALLET,
                checklist =>
                {
                    checklist.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE;
                    checklist.Comment = message;
                });
        service._portalRepositories.GetInstance<IProcessStepRepository>()
            .AttachAndModifyProcessStep(processSteps.Single(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_IDENTITY_WALLET).Id, null, processStep => processStep.ProcessStepStatusId = ProcessStepStatusId.DONE);
        var nextSteps = ScheduleProcessSteps(service, applicationId, processSteps, ProcessStepTypeId.START_CLEARING_HOUSE);
        return (ApplicationChecklistEntryStatusId.DONE, nextSteps, true);
    }

    private static async Task<(ApplicationChecklistEntryStatusId,IEnumerable<ProcessStep>,bool)> HandleClearingHouse(ChecklistService service, Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        var walletData = await service._custodianBusinessLogic.GetWalletByBpnAsync(applicationId, cancellationToken);
        if (walletData == null || string.IsNullOrEmpty(walletData.Did))
        {
            throw new ConflictException($"Decentralized Identifier for application {applicationId} is not set");
        }

        await service._clearinghouseBusinessLogic.TriggerCompanyDataPost(applicationId, walletData.Did, cancellationToken).ConfigureAwait(false);
        service._portalRepositories.GetInstance<IApplicationChecklistRepository>()
            .AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.CLEARING_HOUSE,
                checklist =>
                {
                    checklist.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.IN_PROGRESS;
                });
        service._portalRepositories.GetInstance<IProcessStepRepository>()
            .AttachAndModifyProcessStep(processSteps.Single(step => step.ProcessStepTypeId == ProcessStepTypeId.START_CLEARING_HOUSE).Id, null, processStep => processStep.ProcessStepStatusId = ProcessStepStatusId.DONE);

        ScheduleProcessSteps(service, applicationId, processSteps, ProcessStepTypeId.END_CLEARING_HOUSE);
        return (ApplicationChecklistEntryStatusId.IN_PROGRESS, Enumerable.Empty<ProcessStep>(), true);
    }

    private static async Task<(ApplicationChecklistEntryStatusId,IEnumerable<ProcessStep>,bool)> HandleSelfDescription(ChecklistService service, Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        await service._sdFactoryBusinessLogic
            .RegisterSelfDescriptionAsync(applicationId, cancellationToken)
            .ConfigureAwait(false);
        service._portalRepositories.GetInstance<IApplicationChecklistRepository>()
            .AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP,
                checklist =>
                {
                    checklist.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE;
                });
        return (ApplicationChecklistEntryStatusId.DONE, Enumerable.Empty<ProcessStep>(), true);
    }

    // TODO just for reference - delete is implementation is settled
    private static IEnumerable<ApplicationChecklistEntryTypeId> GetNextPossibleTypesWithMatchingStatus(IDictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId> currentStatus, IEnumerable<ApplicationChecklistEntryStatusId> checklistEntryStatusIds)
    {
        var possibleTypes = new List<ApplicationChecklistEntryTypeId>();
        if (currentStatus.TryGetValue(ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, out var bpnStatus) && checklistEntryStatusIds.Contains(bpnStatus)) // scheduled on creation only if bpn is initially empty
        {
            possibleTypes.Add(ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER);
        }
        if (currentStatus.TryGetValue(ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, out var registrationStatus) && checklistEntryStatusIds.Contains(registrationStatus)) // scheduled on creation
        {
            possibleTypes.Add(ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION);
        }
        if (currentStatus.TryGetValue(ApplicationChecklistEntryTypeId.IDENTITY_WALLET, out var identityStatus) && checklistEntryStatusIds.Contains(identityStatus) && bpnStatus == ApplicationChecklistEntryStatusId.DONE && registrationStatus == ApplicationChecklistEntryStatusId.DONE)
        {
            possibleTypes.Add(ApplicationChecklistEntryTypeId.IDENTITY_WALLET); // scheduled when BUSINESS_PARTNER_NUMBER and REGISTRATION_VERIFICATION are done.
        }
        if (currentStatus.TryGetValue(ApplicationChecklistEntryTypeId.CLEARING_HOUSE, out var clearingHouseStatus) && checklistEntryStatusIds.Contains(clearingHouseStatus) && identityStatus == ApplicationChecklistEntryStatusId.DONE)
        {
            possibleTypes.Add(ApplicationChecklistEntryTypeId.CLEARING_HOUSE); // scheduled when IDENTITY_WALLET is done.
        }
        if (currentStatus.TryGetValue(ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP, out var selfDescriptionStatus) && checklistEntryStatusIds.Contains(selfDescriptionStatus) && clearingHouseStatus == ApplicationChecklistEntryStatusId.DONE)
        {
            possibleTypes.Add(ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP); // scheduled when CLEARING_HOUSE is done.
        }

        return possibleTypes;
    }
}

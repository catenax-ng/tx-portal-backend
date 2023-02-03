/********************************************************************************
 * Copyright (c) 2021,2023 Microsoft and BMW Group AG
 * Copyright (c) 2021,2023 Contributors to the Eclipse Foundation
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
using Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.BusinessLogic;
using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Worker;

public class ChecklistProcessor : IChecklistProcessor
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IBpdmBusinessLogic _bpdmBusinessLogic;
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;
    private readonly IClearinghouseBusinessLogic _clearinghouseBusinessLogic;
    private readonly ISdFactoryBusinessLogic _sdFactoryBusinessLogic;
    private readonly IApplicationActivationService _applicationActivationService;
    private readonly ILogger<IChecklistProcessor> _logger;
    private readonly ImmutableDictionary<ProcessStepTypeId, (ApplicationChecklistEntryTypeId EntryTypeId, Func<IChecklistService.WorkerChecklistProcessStepData,CancellationToken,Task<(Action<ApplicationChecklistEntry>? modifyApplicationChecklistEntry, IEnumerable<ProcessStepTypeId>? NextSteps, bool Modified)>> ProcessFunc)> _stepExecutions;

    public ChecklistProcessor(
        IPortalRepositories portalRepositories,
        IBpdmBusinessLogic bpdmBusinessLogic,
        ICustodianBusinessLogic custodianBusinessLogic,
        IClearinghouseBusinessLogic clearinghouseBusinessLogic,
        ISdFactoryBusinessLogic sdFactoryBusinessLogic,
        IApplicationActivationService applicationActivationService,
        ILogger<IChecklistProcessor> logger)
    {
        _portalRepositories = portalRepositories;
        _bpdmBusinessLogic = bpdmBusinessLogic;
        _custodianBusinessLogic = custodianBusinessLogic;
        _clearinghouseBusinessLogic = clearinghouseBusinessLogic;
        _sdFactoryBusinessLogic = sdFactoryBusinessLogic;
        _applicationActivationService = applicationActivationService;
        _logger = logger;

        _stepExecutions = new (ApplicationChecklistEntryTypeId ApplicationChecklistEntryTypeId, ProcessStepTypeId ProcessStepTypeId, Func<IChecklistService.WorkerChecklistProcessStepData,CancellationToken,Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStepTypeId>?,bool)>> ProcessFunc) []
        {
            new (ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, (context, cancellationToken) => _bpdmBusinessLogic.HandlePullLegalEntity(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ProcessStepTypeId.CREATE_IDENTITY_WALLET, (context, cancellationToken) => _custodianBusinessLogic.CreateIdentityWalletAsync(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ProcessStepTypeId.START_CLEARING_HOUSE, (context, cancellationToken) => _clearinghouseBusinessLogic.HandleStartClearingHouse(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP, ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, (context, cancellationToken) => _sdFactoryBusinessLogic.RegisterSelfDescription(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.APPLICATION_ACTIVATION, ProcessStepTypeId.ACTIVATE_APPLICATION, (context, cancellationToken) => _applicationActivationService.HandleApplicationActivation(context, cancellationToken)),
        }.ToImmutableDictionary(x => x.ProcessStepTypeId, x => (x.ApplicationChecklistEntryTypeId, x.ProcessFunc));
    }

    private static readonly IEnumerable<ProcessStepTypeId> _manuelProcessSteps = new [] {
        ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH,
        ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL,
        ProcessStepTypeId.END_CLEARING_HOUSE,
        ProcessStepTypeId.VERIFY_REGISTRATION,
    };

    public async IAsyncEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId)> ProcessChecklist(Guid applicationId, IEnumerable<(ApplicationChecklistEntryTypeId EntryTypeId, ApplicationChecklistEntryStatusId EntryStatusId)> checklistEntries, IEnumerable<ProcessStep> processSteps, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var checklist = checklistEntries.ToDictionary(entry => entry.EntryTypeId, entry => entry.EntryStatusId);
        var allSteps = processSteps.GroupBy(step => step.ProcessStepTypeId).ToDictionary(group => group.Key, group => group.AsEnumerable());
        var workerStepTypeIds = new Queue<ProcessStepTypeId>(allSteps.Select(step => step.Key).Except(_manuelProcessSteps));
        var manualStepTypeIds = allSteps.Select(step => step.Key).Intersect(_manuelProcessSteps).ToList();
        var checklistRepository = _portalRepositories.GetInstance<IApplicationChecklistRepository>();
        var processStepRepository = _portalRepositories.GetInstance<IProcessStepRepository>();
        _logger.LogInformation("Found {StepsCount} possible steps for application {ApplicationId}", workerStepTypeIds.Count(), applicationId);
        while (workerStepTypeIds.TryDequeue(out var stepTypeId))
        {
            if (_stepExecutions.TryGetValue(stepTypeId, out var execution))
            {
                if (!checklist.TryGetValue(execution.EntryTypeId, out var entryStatusId))
                {
                    throw new ConflictException($"no checklist entry {execution.EntryTypeId} for {stepTypeId}");
                }
                var modified = false;
                try
                {
                    var result = await execution.ProcessFunc(new IChecklistService.WorkerChecklistProcessStepData(applicationId, checklist.ToImmutableDictionary(), workerStepTypeIds.Concat(manualStepTypeIds)), cancellationToken).ConfigureAwait(false);
                    if (result.Modified)
                    {
                        modified |= ModifyStep(stepTypeId, ProcessStepStatusId.DONE, allSteps, processStepRepository);
                        modified |= ScheduleNextSteps(result.NextSteps, allSteps, workerStepTypeIds, manualStepTypeIds, processStepRepository);
                        if (result.modifyApplicationChecklistEntry != null)
                        {
                            var entry = checklistRepository
                                .AttachAndModifyApplicationChecklist(applicationId, execution.EntryTypeId,
                                    result.modifyApplicationChecklistEntry);
                            entryStatusId = entry.ApplicationChecklistEntryStatusId;
                            modified = true;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (ex is not ServiceException { StatusCode: HttpStatusCode.ServiceUnavailable })
                    {
                        ModifyStep(stepTypeId, ProcessStepStatusId.FAILED, allSteps, processStepRepository);
                        entryStatusId = ApplicationChecklistEntryStatusId.FAILED;
                    }

                    checklistRepository.AttachAndModifyApplicationChecklist(applicationId, execution.EntryTypeId,
                            item => { 
                                item.ApplicationChecklistEntryStatusId = entryStatusId;
                                item.Comment = ex.ToString(); 
                            });
                    modified = true;
                }
                if (modified)
                {
                    checklist[execution.EntryTypeId] = entryStatusId;
                    yield return (execution.EntryTypeId, entryStatusId);
                }
            }
            else
            {
                throw new ConflictException($"no execution defined for processStep {stepTypeId}");
            }
        }

        static bool ScheduleNextSteps(IEnumerable<ProcessStepTypeId>? nextSteps, Dictionary<ProcessStepTypeId, IEnumerable<ProcessStep>> allSteps, Queue<ProcessStepTypeId> workerStepTypeIds, List<ProcessStepTypeId> manualStepTypeIds, IProcessStepRepository processStepRepository)
        {
            bool modified = false;
            if (nextSteps != null)
            {
                foreach (var nextStepTypeId in nextSteps.Except(allSteps.Keys))
                {
                    allSteps.Add(nextStepTypeId, new[] { processStepRepository.CreateProcessStep(nextStepTypeId, ProcessStepStatusId.TODO) });
                    if (_manuelProcessSteps.Contains(nextStepTypeId))
                    {
                        manualStepTypeIds.Add(nextStepTypeId);
                    }
                    else
                    {
                        workerStepTypeIds.Enqueue(nextStepTypeId);
                    }
                    modified = true;
                }
            }
            return modified;
        }
    
        static bool ModifyStep(ProcessStepTypeId stepTypeId, ProcessStepStatusId statusId, IDictionary<ProcessStepTypeId,IEnumerable<ProcessStep>> allSteps, IProcessStepRepository processStepRepository)
        {
            if (allSteps.Remove(stepTypeId, out var currentSteps))
            {
                var firstModified = false;
                foreach (var processStep in currentSteps)
                {
                    processStepRepository.AttachAndModifyProcessStep(
                        processStep.Id,
                        null,
                        step => step.ProcessStepStatusId =
                            firstModified
                                ? ProcessStepStatusId.DUPLICATE
                                : statusId);
                    firstModified = true;
                }
                return firstModified;
            }
            return false;
        }
    }
}

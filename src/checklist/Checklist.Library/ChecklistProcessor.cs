/********************************************************************************
 * Copyright (c) 2021,2022 Microsoft and BMW Group AG
 * Copyright (c) 2021,2022 Contributors to the Eclipse Foundation
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
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;

public class ChecklistProcessor : IChecklistProcessor
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IBpdmProcessHandler _bpdmProcessHandler;
    private readonly IIdentityWalletProcessHandler _identityWalletProcessHandler;
    private readonly IClearingHouseProcessHandler _clearingHouseProcessHandler;
    private readonly ISelfDescriptionProcessHander _selfDescriptionProcessHandler;
    private readonly ILogger<IChecklistService> _logger;
    private readonly ImmutableDictionary<ProcessStepTypeId, (ApplicationChecklistEntryTypeId EntryTypeId, Func<Guid,ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId>,IEnumerable<ProcessStep>,CancellationToken,Task<(ApplicationChecklistEntryStatusId ApplicationChecklistEntryStatusId, IEnumerable<ProcessStep> NextSteps, bool Modified)>> ProcessFunc)> _stepExecutions;

    public ChecklistProcessor(
        IPortalRepositories portalRepositories,
        IBpdmProcessHandler bpdmProcessHandler,
        IIdentityWalletProcessHandler identityWalletProcessHandler,
        IClearingHouseProcessHandler clearingHouseProcessHandler,
        ISelfDescriptionProcessHander selfDescriptionProcessHander,
        ILogger<IChecklistService> logger)
    {
        _portalRepositories = portalRepositories;
        _bpdmProcessHandler = bpdmProcessHandler;
        _identityWalletProcessHandler = identityWalletProcessHandler;
        _clearingHouseProcessHandler = clearingHouseProcessHandler;
        _selfDescriptionProcessHandler = selfDescriptionProcessHander;
        _logger = logger;

        _stepExecutions = new (ApplicationChecklistEntryTypeId ApplicationChecklistEntryTypeId, ProcessStepTypeId ProcessStepTypeId, Func<Guid,ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId>,IEnumerable<ProcessStep>,CancellationToken,Task<(ApplicationChecklistEntryStatusId,IEnumerable<ProcessStep>,bool)>> ProcessFunc) []
        {
            new (ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, (Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken) => _bpdmProcessHandler.HandleBpnPull(applicationId, checklist, processSteps, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ProcessStepTypeId.CREATE_IDENTITY_WALLET, (Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken) => _identityWalletProcessHandler.CreateWalletAsync(applicationId, checklist, processSteps, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ProcessStepTypeId.START_CLEARING_HOUSE, (Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken) => _clearingHouseProcessHandler.HandleClearingHouse(applicationId, checklist, processSteps, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP, ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, (Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken) => _selfDescriptionProcessHandler.HandleSelfDescription(applicationId, checklist, processSteps, cancellationToken)),
        }.ToImmutableDictionary(x => x.ProcessStepTypeId, x => (x.ApplicationChecklistEntryTypeId, x.ProcessFunc));
    }

    private static readonly IEnumerable<ProcessStepTypeId> _manuelProcessSteps = new [] {
        ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH,
        ProcessStepTypeId.END_CLEARING_HOUSE,
    };

    public async IAsyncEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId, bool Processed)> ProcessChecklist(Guid applicationId, IEnumerable<(ApplicationChecklistEntryTypeId EntryTypeId, ApplicationChecklistEntryStatusId EntryStatusId)> checklistEntries, IEnumerable<ProcessStep> processSteps, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var checklist = checklistEntries.ToDictionary(entry => entry.EntryTypeId, entry => entry.EntryStatusId);
        var workerSteps = new Queue<ProcessStep>(processSteps.ExceptBy(_manuelProcessSteps, step => step.ProcessStepTypeId));
        var manualSteps = processSteps.IntersectBy(_manuelProcessSteps, step => step.ProcessStepTypeId).ToList();
        var checklistRepository = _portalRepositories.GetInstance<IApplicationChecklistRepository>();
        var processStepRepository = _portalRepositories.GetInstance<IProcessStepRepository>();
        _logger.LogInformation("Found {StepsCount} possible steps for application {ApplicationId}", workerSteps.Count(), applicationId);
        while (workerSteps.TryDequeue(out var step))
        {
            if (_stepExecutions.TryGetValue(step.ProcessStepTypeId, out var execution))
            {
                if (!checklist.TryGetValue(execution.EntryTypeId, out var returnStatusId))
                {
                    throw new ConflictException($"no checklist entry {execution.EntryTypeId} for {step.ProcessStepTypeId}");
                }
                var modified = false;
                try
                {
                    var result = await execution.ProcessFunc(applicationId, checklist.ToImmutableDictionary(), workerSteps.Concat(manualSteps), cancellationToken).ConfigureAwait(false);
                    if (result.Modified)
                    {
                        foreach (var nextStep in result.NextSteps)
                        {
                            if (_manuelProcessSteps.Contains(nextStep.ProcessStepTypeId))
                            {
                                manualSteps.Append(nextStep);
                            }
                            else
                            {
                                workerSteps.Enqueue(nextStep);
                            }
                        }
                        processStepRepository.AttachAndModifyProcessStep(step.Id, null, step => step.ProcessStepStatusId = ProcessStepStatusId.DONE);
                        returnStatusId = result.ApplicationChecklistEntryStatusId;
                        checklist[execution.EntryTypeId] = returnStatusId;
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
}

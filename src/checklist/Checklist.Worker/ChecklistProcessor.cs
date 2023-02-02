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
    private readonly ILogger<IChecklistService> _logger;
    private readonly ImmutableDictionary<ProcessStepTypeId, (ApplicationChecklistEntryTypeId EntryTypeId, Func<IChecklistService.WorkerChecklistProcessStepData,CancellationToken,Task<(Action<ApplicationChecklistEntry>? modifyApplicationChecklistEntry, IEnumerable<ProcessStep>? NextSteps, bool Modified)>> ProcessFunc)> _stepExecutions;

    public ChecklistProcessor(
        IPortalRepositories portalRepositories,
        IBpdmBusinessLogic bpdmBusinessLogic,
        ICustodianBusinessLogic custodianBusinessLogic,
        IClearinghouseBusinessLogic clearinghouseBusinessLogic,
        ISdFactoryBusinessLogic sdFactoryBusinessLogic,
        IApplicationActivationService applicationActivationService,
        ILogger<IChecklistService> logger)
    {
        _portalRepositories = portalRepositories;
        _bpdmBusinessLogic = bpdmBusinessLogic;
        _custodianBusinessLogic = custodianBusinessLogic;
        _clearinghouseBusinessLogic = clearinghouseBusinessLogic;
        _sdFactoryBusinessLogic = sdFactoryBusinessLogic;
        _applicationActivationService = applicationActivationService;
        _logger = logger;

        _stepExecutions = new (ApplicationChecklistEntryTypeId ApplicationChecklistEntryTypeId, ProcessStepTypeId ProcessStepTypeId, Func<IChecklistService.WorkerChecklistProcessStepData,CancellationToken,Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStep>?,bool)>> ProcessFunc) []
        {
            new (ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, (IChecklistService.WorkerChecklistProcessStepData context, CancellationToken cancellationToken) => _bpdmBusinessLogic.HandlePullLegalEntity(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ProcessStepTypeId.CREATE_IDENTITY_WALLET, (IChecklistService.WorkerChecklistProcessStepData context, CancellationToken cancellationToken) => _custodianBusinessLogic.CreateIdentityWalletAsync(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ProcessStepTypeId.START_CLEARING_HOUSE, (IChecklistService.WorkerChecklistProcessStepData context, CancellationToken cancellationToken) => _clearinghouseBusinessLogic.HandleStartClearingHouse(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP, ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, (IChecklistService.WorkerChecklistProcessStepData context, CancellationToken cancellationToken) => _sdFactoryBusinessLogic.RegisterSelfDescription(context, cancellationToken)),
            new (ApplicationChecklistEntryTypeId.APPLICATION_ACTIVATION, ProcessStepTypeId.ACTIVATE_APPLICATION, (IChecklistService.WorkerChecklistProcessStepData context, CancellationToken cancellationToken) => _applicationActivationService.HandleApplicationActivation(context, cancellationToken)),
        }.ToImmutableDictionary(x => x.ProcessStepTypeId, x => (x.ApplicationChecklistEntryTypeId, x.ProcessFunc));
    }

    private static readonly IEnumerable<ProcessStepTypeId> _manuelProcessSteps = new [] {
        ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH,
        ProcessStepTypeId.END_CLEARING_HOUSE,
        ProcessStepTypeId.VERIFY_REGISTRATION,
    };

    public async IAsyncEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId)> ProcessChecklist(Guid applicationId, IEnumerable<(ApplicationChecklistEntryTypeId EntryTypeId, ApplicationChecklistEntryStatusId EntryStatusId)> checklistEntries, IEnumerable<ProcessStep> processSteps, [EnumeratorCancellation] CancellationToken cancellationToken)
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
                    var result = await execution.ProcessFunc(new IChecklistService.WorkerChecklistProcessStepData(applicationId, checklist.ToImmutableDictionary(), workerSteps.Concat(manualSteps)), cancellationToken).ConfigureAwait(false);
                    if (result.Modified)
                    {
                        if (result.NextSteps != null)
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
                        }
                        processStepRepository.AttachAndModifyProcessStep(step.Id, null, step => step.ProcessStepStatusId = ProcessStepStatusId.DONE);
                        if (result.modifyApplicationChecklistEntry != null)
                        {
                            var entry = checklistRepository
                                .AttachAndModifyApplicationChecklist(applicationId, execution.EntryTypeId,
                                    result.modifyApplicationChecklistEntry);
                            returnStatusId = entry.ApplicationChecklistEntryStatusId;                            
                            checklist[execution.EntryTypeId] = returnStatusId;
                        }                        
                        modified = true;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (ex is not ServiceException { StatusCode: HttpStatusCode.ServiceUnavailable })
                    {
                        processStepRepository.AttachAndModifyProcessStep(step.Id, null, step => step.ProcessStepStatusId = ProcessStepStatusId.FAILED);
                        returnStatusId = ApplicationChecklistEntryStatusId.FAILED;
                    }

                    checklistRepository.AttachAndModifyApplicationChecklist(applicationId, execution.EntryTypeId,
                            item => { 
                                item.ApplicationChecklistEntryStatusId = returnStatusId;
                                item.Comment = ex.ToString(); 
                            });
                    modified = true;
                }
                if (modified)
                {
                    yield return (execution.EntryTypeId, returnStatusId);
                }
            }
            else
            {
                throw new ConflictException($"no execution defined for processStep {step.ProcessStepTypeId}");
            }
        }
    }
}

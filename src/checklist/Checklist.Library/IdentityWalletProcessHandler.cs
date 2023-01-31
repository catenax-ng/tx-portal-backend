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

using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;

public class IdentityWalletProcessHandler : IIdentityWalletProcessHandler
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;
    private readonly IChecklistService _checklistService;

    public IdentityWalletProcessHandler(
        IPortalRepositories portalRepositories,
        ICustodianBusinessLogic custodianBusinessLogic,
        IChecklistService checklistService)
    {
        _portalRepositories = portalRepositories;
        _custodianBusinessLogic = custodianBusinessLogic;
        _checklistService = checklistService;
    }

    public async Task<(ApplicationChecklistEntryStatusId,IEnumerable<ProcessStep>,bool)> CreateWalletAsync(Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        var message = await _custodianBusinessLogic.CreateWalletAsync(applicationId, cancellationToken).ConfigureAwait(false);
        _portalRepositories.GetInstance<IApplicationChecklistRepository>()
            .AttachAndModifyApplicationChecklist(applicationId, ApplicationChecklistEntryTypeId.IDENTITY_WALLET,
                checklist =>
                {
                    checklist.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE;
                    checklist.Comment = message;
                });
        _portalRepositories.GetInstance<IProcessStepRepository>()
            .AttachAndModifyProcessStep(processSteps.Single(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_IDENTITY_WALLET).Id, null, processStep => processStep.ProcessStepStatusId = ProcessStepStatusId.DONE);
        var nextSteps = _checklistService.ScheduleProcessSteps(applicationId, processSteps, ProcessStepTypeId.START_CLEARING_HOUSE);
        return (ApplicationChecklistEntryStatusId.DONE, nextSteps, true);
    }
}

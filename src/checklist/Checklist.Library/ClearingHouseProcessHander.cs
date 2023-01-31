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

using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;

public class ClearingHouseProcessHandler : IClearingHouseProcessHandler
{
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;
    private readonly IClearinghouseBusinessLogic _clearinghouseBusinessLogic;
    private readonly IChecklistService _checklistService;

    public ClearingHouseProcessHandler(ICustodianBusinessLogic custodianBusinessLogic, IClearinghouseBusinessLogic clearinghouseBusinessLogic, IChecklistService checklistService)
    {
        _custodianBusinessLogic = custodianBusinessLogic;
        _clearinghouseBusinessLogic = clearinghouseBusinessLogic;
        _checklistService = checklistService;
    }

    public async Task ProcessEndClearinghouse(Guid applicationId, CancellationToken cancellationToken)
    {
        var follupUpStep = new [] { ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP };
        var processStepId = await _checklistService.VerifyChecklistEntryAndProcessSteps(applicationId, ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ProcessStepTypeId.END_CLEARING_HOUSE, follupUpStep).ConfigureAwait(false);

        // TODO implement call to businessLogic end clearinghouse 

        _checklistService.FinalizeChecklistEntryAndProcessSteps(applicationId, ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ApplicationChecklistEntryStatusId.DONE, processStepId, follupUpStep);
    }

    public async Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStep>?,bool)> HandleClearingHouse(Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        var walletData = await _custodianBusinessLogic.GetWalletByBpnAsync(applicationId, cancellationToken);
        if (walletData == null || string.IsNullOrEmpty(walletData.Did))
        {
            throw new ConflictException($"Decentralized Identifier for application {applicationId} is not set");
        }

        await _clearinghouseBusinessLogic.TriggerCompanyDataPost(applicationId, walletData.Did, cancellationToken).ConfigureAwait(false);

        _checklistService.ScheduleProcessSteps(applicationId, processSteps, ProcessStepTypeId.END_CLEARING_HOUSE);
        return (entry => entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.IN_PROGRESS, null, true);
    }
}

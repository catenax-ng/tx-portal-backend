/********************************************************************************
 * Copyright (c) 2021,2022 BMW Group AG
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

using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;

/// <summary>
/// Repository for accessing and creating processSteps on persistence layer.
/// </summary>
public interface IProcessStepRepository
{
    ProcessStep CreateProcessStep(ProcessStepTypeId processStepTypeId, ProcessStepStatusId processStepStatusId);
    void AttachAndModifyProcessStep(Guid processStepId, Action<ProcessStep>? initialize, Action<ProcessStep> modify);
    
    /// <summary>
    /// Checks whether the process step is in state TODO for the given application 
    /// </summary>
    /// <param name="applicationId">Id of the application</param>
    /// <param name="processStep">the process step to check</param>
    /// <returns><c>true</c> if the processStep is in state todo, otherwise <c>false</c></returns>
    Task<bool> GetProcessStepByApplicationIdInStatusTodo(Guid applicationId, ProcessStepTypeId processStep);
}

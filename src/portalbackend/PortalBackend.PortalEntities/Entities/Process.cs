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

using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Base;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;

public class Process : IBaseEntity
{
    private Process()
    {
        ProcessSteps = new HashSet<ProcessStep>();
        CompanyApplications = new HashSet<CompanyApplication>();
    }

    public Process(Guid id, ProcessTypeId processTypeId) : this()
    {
        Id = id;
        ProcessTypeId = processTypeId;
    }

    public Guid Id { get; private set; }
    public ProcessTypeId ProcessTypeId { get; set; }

    // Navigation properties
    public virtual ProcessType? ProcessType { get; set; }
    public virtual ICollection<ProcessStep> ProcessSteps { get; private set; }
    public virtual ICollection<CompanyApplication> CompanyApplications { get; private set; }
}

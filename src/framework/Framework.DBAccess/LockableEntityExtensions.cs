/********************************************************************************
 * Copyright (c) 2022 BMW Group AG
 * Copyright (c) 2022 Contributors to the Eclipse Foundation
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

namespace Org.Eclipse.TractusX.Portal.Backend.Framework.DBAccess;

public static class LockableEntityExtensions
{
    public static bool TryLock(this ILockableEntity entity, DateTimeOffset lockExpiryDate)
    {
        if (entity.IsLocked())
        {
            return false;
        }
        entity.Version = Guid.NewGuid();
        entity.LockExpiryDate = lockExpiryDate;
        return true;
    }

    public static bool ReleaseLock(this ILockableEntity entity)
    {
        if (entity.IsLocked())
        {
            entity.Version = Guid.NewGuid();
            entity.LockExpiryDate = null;
            return true;
        }
        return false;
    }

    public static bool IsLocked(this ILockableEntity entity) => entity.LockExpiryDate != null;

    public static bool IsLockExpired(this ILockableEntity entity, DateTimeOffset utcNow) => entity.LockExpiryDate < utcNow;
}

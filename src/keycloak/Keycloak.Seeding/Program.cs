/********************************************************************************
 * Copyright (c) 2021, 2023 BMW Group AG
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
// See https://aka.ms/new-console-template for more information

Console.WriteLine("Starting process");
try
{
    // var builder = Host.CreateDefaultBuilder(args)
    //     .ConfigureServices((hostContext, services) =>
    //     {
    //     });

    // var host = builder.Build();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("StopTheHostException", StringComparison.Ordinal))
{
    // Should be replaced with Serilog as soon as we have it.
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Unhandled exception: {0}", ex);
    Console.ResetColor();
    throw;
}
finally
{
    // Should be replaced with Serilog as soon as we have it.
    Console.WriteLine("Process Shutting down...");
}
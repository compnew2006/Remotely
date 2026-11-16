#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.API;
using Remotely.Server.Auth;
using Remotely.Server.Filters;

namespace Remotely.Server.Tests;

/// <summary>
/// Every endpoint of the API has to state how it is protected.  A new
/// controller that is added without an authorization decision would otherwise
/// be reachable by anyone who knows its route.
/// </summary>
[TestClass]
public class ApiSurfaceTests
{
    /// <summary>
    /// The controllers that are reachable without an API key or a session by
    /// design.  Every one of them is listed here on purpose: a new entry has
    /// to be a decision, not an accident.
    /// </summary>
    private static readonly HashSet<string> PublicByDesign =
    [
        // The agent downloads the client packages and the signed manifests.
        "AgentUpdateController",
        // The branding information is read by the login page.
        "BrandingController",
        // The legacy login endpoint.
        "LoginController"
    ];

    private static readonly Type[] AuthorizationFilters =
    [
        typeof(ApiAuthorizationFilter),
        typeof(ExpiringTokenFilter),
        typeof(LocalOnlyFilter),
        typeof(ViewerAuthorizationFilter)
    ];

    [TestMethod]
    public void EveryApiController_HasAnAuthorizationDecision()
    {
        var controllerTypes = GetApiControllers();

        Assert.IsTrue(
            controllerTypes.Length >= 15,
            $"Only {controllerTypes.Length} API controllers were found - the discovery is broken.");

        var unprotected = new List<string>();

        foreach (var controllerType in controllerTypes)
        {
            if (PublicByDesign.Contains(controllerType.Name))
            {
                continue;
            }

            foreach (var action in GetPublicActions(controllerType))
            {
                if (!IsProtected(controllerType, action))
                {
                    unprotected.Add($"{controllerType.Name}.{action.Name}");
                }
            }
        }

        Assert.AreEqual(
            0,
            unprotected.Count,
            "These actions have no authorization decision: " +
            string.Join(", ", unprotected));
    }

    [TestMethod]
    public void ThePublicByDesignList_IsAccurate()
    {
        var controllerTypes = GetApiControllers();

        foreach (var name in PublicByDesign)
        {
            var controllerType = controllerTypes.SingleOrDefault(x => x.Name == name);

            Assert.IsNotNull(
                controllerType,
                $"'{name}' is on the public list but no such controller exists.");
        }

        // A controller that is on the list has to be really reachable without
        // a filter, otherwise the list hides a protected controller.
        foreach (var controllerType in controllerTypes.Where(x => PublicByDesign.Contains(x.Name)))
        {
            var hasOpenAction = GetPublicActions(controllerType)
                .Any(action => !IsProtected(controllerType, action));

            Assert.IsTrue(
                hasOpenAction,
                $"'{controllerType.Name}' is on the public list, but every one of its " +
                "actions is protected.  Remove it from the list or protect the action.");
        }
    }

    private static Type[] GetApiControllers()
    {
        return typeof(HealthCheckController).Assembly
            .GetTypes()
            .Where(x => typeof(ControllerBase).IsAssignableFrom(x) && !x.IsAbstract)
            .OrderBy(x => x.Name)
            .ToArray();
    }

    private static MethodInfo[] GetPublicActions(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(x => !x.IsSpecialName && x.ReturnType != typeof(void))
            .Where(x => x.GetCustomAttributes().All(a => a.GetType().Name != "NonActionAttribute"))
            .ToArray();
    }

    private static bool IsProtected(Type controllerType, MethodInfo action)
    {
        return HasAuthorizationAttribute(controllerType) ||
               HasAuthorizationAttribute(action) ||
               HasAuthorizationFilter(controllerType) ||
               HasAuthorizationFilter(action) ||
               HasInternalAuthorization(action);
    }

    private static bool HasInternalAuthorization(MemberInfo member)
    {
        return member.GetCustomAttributes(typeof(InternallyAuthorizedAttribute), true).Length > 0;
    }

    private static bool HasAuthorizationAttribute(MemberInfo member)
    {
        return member.GetCustomAttributes(typeof(AuthorizeAttribute), true).Length > 0;
    }

    private static bool HasAuthorizationFilter(MemberInfo member)
    {
        foreach (var attribute in member
            .GetCustomAttributes(typeof(ServiceFilterAttribute), true)
            .OfType<ServiceFilterAttribute>())
        {
            // ServiceFilterAttribute keeps the implementation type private, so
            // every field is inspected instead of relying on its name.
            foreach (var field in typeof(ServiceFilterAttribute)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.GetValue(attribute) is Type implementationType &&
                    AuthorizationFilters.Contains(implementationType))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgeSetup;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges;
﻿using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ReconcilePaymentWebhook;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Pledges.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class PledgesApplicationDependencyInjection
{
    /// <summary>
    /// Registers Pledges application services, command handlers, and query handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddPledgesApplication(this IServiceCollection services)
    {
        services.AddScoped<ConfirmPledgePaymentCommandHandler>();
        services.AddScoped<FailPledgePaymentCommandHandler>();
        services.AddScoped<MakePledgeCommandHandler>();
        services.AddScoped<PledgeRefunder>();
        services.AddScoped<PledgeChargeService>();
        services.AddScoped<PayoutService>();
        services.AddScoped<RefundJobService>();
        services.AddScoped<ConfirmPledgeSetupCommandHandler>();
        services.AddScoped<IValidator<ConfirmPledgeSetupCommand>, ConfirmPledgeSetupCommandValidator>();
        services.AddScoped<ReconcilePaymentWebhookCommandHandler>();
        services.AddScoped<ListPledgesByCampaignQueryHandler>();
        services.AddScoped<IValidator<ConfirmPledgePaymentCommand>, ConfirmPledgePaymentCommandValidator>();
        services.AddScoped<IValidator<FailPledgePaymentCommand>, FailPledgePaymentCommandValidator>();
        services.AddScoped<IValidator<MakePledgeCommand>, MakePledgeCommandValidator>();
        services.AddScoped<IValidator<ReconcilePaymentWebhookCommand>, ReconcilePaymentWebhookCommandValidator>();

        return services;
    }
}

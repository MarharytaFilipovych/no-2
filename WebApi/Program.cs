using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.API.System;
using Application.Api.Utils;
using Application.Auth;
using Application.Commands.Auth;
using Application.Utils;
using Application.Validators.Auctions;
using Application.Validators.Auth;
using Infrastructure.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Application.Configs;
using Application.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
builder.Services.AddSingleton<ITimeProvider, UtcTimeProvider>();

builder.Services.AddScoped<ICreateAuctionValidator, AuctionTitleValidator>();
builder.Services.AddScoped<ICreateAuctionValidator, AuctionTimeRangeValidator>();
builder.Services.AddScoped<ICreateAuctionValidator, OpenAuctionRequiresIncrementValidator>();

builder.Services.AddScoped<IBidValidator, MaxBidAmountValidator>();
builder.Services.AddScoped<IBidValidator, BalanceRatioValidator>();
builder.Services.AddScoped<IBidValidator, OpenAuctionIncrementValidator>();
builder.Services.AddScoped<IBidValidator, BlindAuctionSingleBidValidator>();

builder.Services.AddScoped<IWithdrawBidValidator, BidOwnershipValidator>();
builder.Services.AddScoped<IWithdrawBidValidator, BidNotAlreadyWithdrawnValidator>();
builder.Services.AddScoped<IWithdrawBidValidator, AuctionActiveForWithdrawalValidator>();

builder.Services.AddScoped<IRegisterValidator, EmailAlreadyExistsValidator>();
builder.Services.AddScoped<ILoginValidator, UserCredentialsValidator>();
builder.Services.AddScoped<IRefreshTokenValidator, SessionExistsValidator>();
builder.Services.AddScoped<IRefreshTokenValidator, RefreshTokenMatchValidator>();

builder.Services.AddSingleton<Domain.Auctions.AuctionVisibilityService>();
builder.Services.AddSingleton<Domain.Auctions.WinnerSelectionService>();
builder.Services.AddSingleton<Domain.Auctions.NoRepeatWinnerPolicy>();
builder.Services.AddSingleton<Domain.Auctions.PaymentProcessingService>();
builder.Services.AddSingleton<Domain.Users.BanPolicy>();

builder.InstallConfigFromSection<IPaymentWindowConfig, PaymentWindowConfig>("PaymentWindow");   

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllForTesting",
        policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerAuth();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<RegisterCommand>());

builder.Services.AddPersistence();

builder.Services.AddSingleton<Domain.Auctions.AuctionVisibilityService>();
builder.Services.AddSingleton<Domain.Auctions.WinnerSelectionService>();

var jwtTokenConfig = builder.InstallConfigFromSection<IJwtTokenConfig, JwtTokenConfig>("JwtToken");
builder.InstallConfigFromSection<IBiddingConfig, BiddingConfig>("BiddingConfig");

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtTokenConfig.Secret).ToArray());
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtTokenConfig.Issuer,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = symmetricSecurityKey,
        ValidAudience = jwtTokenConfig.Audience,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAllForTesting");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
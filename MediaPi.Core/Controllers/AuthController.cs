// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaPi.Core.Controllers;

/// <summary>
/// AuthController handles user authentication and authorization operations for the Media Pi application.
/// This controller provides endpoints for user login, authentication status verification, and SSH device authorization.
/// 
/// Key responsibilities:
/// - User authentication via email/password credentials
/// - JWT token generation and validation
/// - Role-based access control verification
/// - Account information retrieval for authenticated users
/// - SSH device authorization for gateway services
/// </summary>
/// <remarks>
/// This controller uses BCrypt for password hashing and verification, and JWT tokens for maintaining
/// user sessions. User roles and account associations are loaded during authentication to provide
/// complete user context in the response.
/// 
/// The SSH authorization functionality allows secure gateway services to verify device access
/// using shared bearer token authentication.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext db, 
    IJwtUtils jwtUtils,
    IOptions<AppSettings> appSettings,
    ILogger<AuthController> logger) : FuelfluxControllerPreBase(db, logger)
{
    /// <summary>
    /// JWT utilities service for generating and validating authentication tokens
    /// </summary>
    private readonly IJwtUtils _jwtUtils = jwtUtils;
    
    /// <summary>
    /// Application settings containing security tokens and configuration
    /// </summary>
    private readonly AppSettings _appSettings = appSettings.Value;

    /// <summary>
    /// Authenticates a user with email and password credentials and returns a JWT token.
    /// 
    /// This endpoint performs the following operations:
    /// 1. Validates user credentials against the database
    /// 2. Verifies the user has at least one assigned role
    /// 3. Generates a JWT token for successful authentication
    /// 4. Returns user information including roles and account associations
    /// 
    /// For users with AccountManager role, the response includes their associated account IDs.
    /// For other roles (SystemAdministrator, InstallationEngineer), AccountIds will be empty.
    /// </summary>
    /// <param name="crd">User credentials containing email and password</param>
    /// <returns>
    /// Success: UserViewItemWithJWT containing user information, roles, account IDs, and JWT token
    /// Failure: 401 Unauthorized for invalid credentials, 403 Forbidden for users without roles
    /// </returns>
    /// <remarks>
    /// The method performs case-insensitive email comparison and uses BCrypt for secure password verification.
    /// User roles and account associations are eagerly loaded to provide complete context in the response.
    /// Debug logging is performed for security auditing purposes.
    /// </remarks>
    // POST: api/auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserViewItemWithJWT))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    public async Task<ActionResult<UserViewItem>> Login(Credentials crd, CancellationToken ct = default)
    {
        // Log the authentication attempt for security auditing
        _logger.LogDebug("Login attempt for {email}", crd.Email);

        // Query user with all necessary related data for complete authentication context
        // Include UserRoles and Role for permission checking
        // Include UserAccounts and Account for account association information
        User? user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.UserAccounts)
            .ThenInclude(ua => ua.Account)
            .Where(u => u.Email.ToLower() == crd.Email.ToLower()) // Case-insensitive email comparison
            .SingleOrDefaultAsync(ct);

        // Return 401 if user doesn't exist (same response as invalid password for security)
        if (user == null) return _401();

        // Verify password using BCrypt (secure password hashing)
        if (!BCrypt.Net.BCrypt.Verify(crd.Password, user.Password)) return _401();
        
        // Ensure user has at least one role assigned (business rule requirement)
        if (!user.HasAnyRole()) return _403();

        // Create response object with user information and generate JWT token
        UserViewItemWithJWT userViewItem = new(user)
        {
            Token = _jwtUtils.GenerateJwtToken(user),
        };

        // Log successful authentication for security auditing
        _logger.LogDebug("Login returning\n{res}", userViewItem.ToString());
        return userViewItem;
    }

    /// <summary>
    /// Verifies the current user's authentication status without returning user data.
    /// 
    /// This endpoint is used to check if a user's JWT token is still valid and they
    /// remain authenticated. It's typically called by client applications to verify
    /// session validity before making other API calls.
    /// 
    /// The endpoint requires authentication (via [Authorize] attribute on the controller),
    /// so a valid JWT token must be present in the request headers.
    /// </summary>
    /// <returns>
    /// Success: 204 No Content - User is authenticated and authorized
    /// Failure: 401 Unauthorized - Invalid or missing JWT token
    /// </returns>
    /// <remarks>
    /// This is a lightweight endpoint that doesn't perform database queries or return
    /// user data. It relies on the JWT validation middleware to determine authentication status.
    /// Used for session validation and keeping user sessions active.
    /// </remarks>
    // GET: api/auth/check
    [HttpGet("check")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    public IActionResult Check()
    {
        // Log the authentication check for debugging purposes
        _logger.LogDebug("Check authorization status");
        
        // Return 204 No Content to indicate successful authentication
        // The [Authorize] attribute ensures only authenticated users reach this point
        return NoContent();
    }

}

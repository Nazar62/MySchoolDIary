using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Validations.Rules;
using MySchoolDiary.Data;
using MySchoolDiary.Models;
using MySchoolDiary.Models.Requests;
using MySchoolDiary.Models.Results;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

//TODO: Implement Refresh token by video https://www.youtube.com/watch?v=2_H0Zj-C8EM&t=207s i stopped on 1:05:45

namespace MySchoolDiary.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        //TODO: Add Repo for Jwt and RefreshToken
        private readonly IConfiguration _configuration;
        private readonly UserManager<User> _userManager;
        private readonly TokenValidationParameters _tokenValidationParameters;
        private readonly AppDbContext _context;


        public AuthController(
            IConfiguration configuration, 
            UserManager<User> userManager,
            TokenValidationParameters tokenValidationParameters,
            AppDbContext context
            )
        {
            _configuration = configuration;
            _userManager = userManager;
            _tokenValidationParameters = tokenValidationParameters;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] UserRegisterRequest request)
        {
            if(!ModelState.IsValid)
                return BadRequest(ModelState);

            var userExists = await _userManager.FindByNameAsync(request.UserName);

            if(userExists != null)
            {
                return BadRequest(new AuthResult
                {
                    Successfully = false,
                    Errors = new List<string>()
                    {
                        "User name alredy used"
                    }
                });
            }

            var newUser = new User()
            {
                UserName = request.UserName,
                Name = request.Name,
                Surname = request.Surname,
                FatherName = request.FatherName,
                Form = request.Form
            };

            var isCreated = await _userManager.CreateAsync(newUser, request.Password);

            if(isCreated.Succeeded)
            {
                var token = await CreateJwtToken(newUser);
                return Ok(token);
            } else
            {
                List<string> errors = isCreated.Errors.Select(x => x.Description.ToString()).ToList();
                return BadRequest(new AuthResult()
                {
                    Successfully = false,
                    Errors = errors
                });
            }

            //return BadRequest(new AuthResult()
            //{
            //    Successfully = false,
            //    Errors = new List<string>()
            //    {
            //        "Server error"
            //    }
            //});

            //user.UserName = request.UserName;
            //user.PasswordHash = passwordHash;

            //return Ok(user);
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] UserRequest request)
        {
            if (!ModelState.IsValid)
                BadRequest(ModelState);

            var existingUser =await  _userManager.FindByNameAsync(request.UserName);

            if (existingUser == null)
                return BadRequest(new AuthResult()
                {
                    Successfully = false,
                    Errors = new List<string>()
                    {
                        "User not exists"
                    }
                });

            var isPasswordCorrect = await _userManager.CheckPasswordAsync(existingUser, request.Password);

            if(!isPasswordCorrect)
            {
                return BadRequest(new AuthResult()
                {
                    Successfully = false,
                    Errors = new List<string>()
                    {
                        "Wrong password"
                    }
                });
            }

            var token = await CreateJwtToken(existingUser);

            //var refreshToken = GenerateRefreshToken();
            //SetRefreshToken(refreshToken);

            return Ok(token);
        }

        [HttpPost("CheckRole")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult> CheckRole()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            return Ok(role);
        }

        private async Task<AuthResult> CreateJwtToken(User user)
        {
            var userRoles = _userManager.GetRolesAsync(user).Result;
            if(userRoles.Count == 0)
            {
                var roleAdded = _userManager.AddToRoleAsync(user, "Student");
                await _context.SaveChangesAsync();
            }
            var roles = _userManager.GetRolesAsync(user).Result;
            List<Claim> claims = new List<Claim>
            {
                new Claim("Id", user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, roles[0]),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration.GetSection("AppSetting:Token").Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
                );

            var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

            var refreshToken = new RefreshToken()
            {
                JwtId = token.Id,
                Token = RandomStringGenerator(22),
                Created = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddMonths(6),
                UserId = user.Id
            };

            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();

            var result = new AuthResult()
            {
                Successfully = true,
                Token = jwtToken,
                RefreshToken = refreshToken.Token
            };
            return result;
        }

        [HttpPost("RefreshToken")]
        public async Task<ActionResult> RefreshToken([FromBody] TokenRequest tokenRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = VerifyAndGenerateToken(tokenRequest);

            if(result == null)
            {
                return BadRequest(new AuthResult()
                {
                    Successfully = false,
                    Errors = new List<string> { "Invalid Tokens" }
                });
            }
            return Ok(result);
        }

        private async Task<AuthResult> VerifyAndGenerateToken(TokenRequest tokenRequest)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            try
            {
                _tokenValidationParameters.ValidateLifetime = false; //TODO: for testing

                var tokenVerification = jwtTokenHandler.ValidateToken(tokenRequest.Token, _tokenValidationParameters, out var validatedToken);

                if(validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256Signature, 
                        StringComparison.InvariantCultureIgnoreCase);

                    if (result == null)
                        return null;
                }

                var utcExpDate = long.Parse(tokenVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp).Value);

                var expDate = UnixTimeStampToDateTime(utcExpDate);

                if(expDate < DateTime.Now)
                    return new AuthResult()
                    {
                        Successfully = false,
                        Errors = new List<string>{"Expired token" }
                    };

                var storedToken = await _context.RefreshTokens.Where(x => x.Token == tokenRequest.RefreshToken).FirstOrDefaultAsync();

                if (storedToken == null)
                    return new AuthResult()
                    {
                        Successfully = false,
                        Errors = new List<string> { "Invalid refresh token" }
                    };

                if (storedToken.IsUsed)
                    return new AuthResult()
                    {
                        Successfully = false,
                        Errors = new List<string> { "Invalid refresh token" }
                    };

                var jti = tokenVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti).Value;

                if(storedToken.JwtId != jti)
                    return new AuthResult()
                    {
                        Successfully = false,
                        Errors = new List<string> { "Invalid refresh token" }
                    };

                if (storedToken.Expires < DateTime.UtcNow)
                    return new AuthResult()
                    {
                        Successfully = false,
                        Errors = new List<string> { "Expired refresh token" }
                    };

                storedToken.IsUsed = true;

                if(_context.RefreshTokens != null)
                {
                    _context.RefreshTokens.Update(storedToken);
                    await _context.SaveChangesAsync(); //error disaposed context
                }

                var dbUser = await _userManager.FindByIdAsync(storedToken.UserId);

                return await CreateJwtToken(dbUser);
            }
            catch (Exception e)
            {
                return new AuthResult()
                {
                    Successfully = false,
                    Errors = new List<string>{ "Server error" }
                };
            }
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0,DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp).ToUniversalTime();

            return dateTimeVal;
        }

        private string RandomStringGenerator(int length)
        {
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890abcdefghigklmnopqrstuvwxyz_";
            return new string(Enumerable.Repeat(chars, length).Select(s => chars[random.Next(length)]).ToArray());
        }
    }
}

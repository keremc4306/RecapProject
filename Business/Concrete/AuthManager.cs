﻿using Business.Abstract;
using Business.Constants;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Transaction;
using Core.Aspects.Autofac.Validation;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.Hashing;
using Core.Utilities.Security.Jwt;
using Entities.Concrete;
using Entities.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private readonly IUserService _userService;
        private readonly ITokenHelper _tokenHelper;
        private readonly ICustomerService _customerService;


        public AuthManager(IUserService userService, ITokenHelper tokenHelper, ICustomerService customerService)
        {
            _userService = userService;
            _tokenHelper = tokenHelper;
            _customerService = customerService;
        }

        [ValidationAspect(typeof(UserRegisterValidator))]
        public IDataResult<User> Register(UserForRegisterDto userForRegisterDto, string password)
        {
            byte[] passwordHash, passwordSalt;
            HashingHelper.CreatePasswordHash(password, out passwordHash, out passwordSalt);
            var user = new User
            {
                Email = userForRegisterDto.Email,
                FirstName = userForRegisterDto.FirstName,
                LastName = userForRegisterDto.LastName,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Status = true
            };
            _userService.Add(user);
            return new SuccessDataResult<User>(user, Messages.UserRegistered);
        }

        public IDataResult<User> Login(UserForLoginDto userForLoginDto)
        {
            var userToCheck = _userService.GetByMail(userForLoginDto.Email).Data;
            if (userToCheck == null)
            {
                return new ErrorDataResult<User>(Messages.UserNotFound);
            }

            if (!HashingHelper.VerifyPasswordHash(userForLoginDto.Password, userToCheck.PasswordHash, userToCheck.PasswordSalt))
            {
                return new ErrorDataResult<User>(Messages.PasswordError);
            }

            return new SuccessDataResult<User>(userToCheck, Messages.SuccessfulLogin);
        }

        public IResult UserExists(string email)
        {
            if (_userService.GetByMail(email).Data != null)
            {
                return new ErrorResult(Messages.UserAlreadyExists);
            }
            return new SuccessResult();
        }

        public IDataResult<AccessToken> CreateAccessToken(User user)
        {
            var claims = _userService.GetClaims(user).Data;
            var accessToken = _tokenHelper.CreateToken(user, claims);
            return new SuccessDataResult<AccessToken>(accessToken, Messages.AccessTokenCreated);
        }

        [ValidationAspect(typeof(CustomerUpdateValidator))]
        [TransactionScopeAspect]
        public IDataResult<UserForUpdateDto> Update(UserForUpdateDto userForUpdate)
        {
            var currentUser = _userService.GetById(userForUpdate.Id);

            var user = new User
            {
                Id = userForUpdate.UserId,
                Email = userForUpdate.Email,
                FirstName = userForUpdate.FirstName,
                LastName = userForUpdate.LastName,
                PasswordHash = currentUser.Data.PasswordHash,
                PasswordSalt = currentUser.Data.PasswordSalt
            };

            byte[] passwordHash, passwordSalt;

            if (userForUpdate.Password != "")
            {
                HashingHelper.CreatePasswordHash(userForUpdate.Password, out passwordHash, out passwordSalt);

                user.PasswordHash = passwordHash;
                user.PasswordSalt = passwordSalt;
            }

            _userService.Update(user);

            var customer = new Customer
            {
                Id = userForUpdate.Id,
                UserId = userForUpdate.UserId
            };

            _customerService.Update(customer);

            return new SuccessDataResult<UserForUpdateDto>(userForUpdate, Messages.CustomerUpdated);
        }
    }
}

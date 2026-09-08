# API Request Validation

## Purpose
Provides model binding and validation helpers bridging FluentValidation result structures into ASP.NET Core MVC ModelState dictionaries.

## Files
- `ValidationExtensions.cs`: Extension methods (`AddToModelState`) mapping validation error properties and messages into `ModelStateDictionary` to return uniform HTTP 400 Bad Request responses.

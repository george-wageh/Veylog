using System.Security.Cryptography;

namespace Veylog
{
    public class VeylogTokenManager
    {
        private string? _token;
        private DateTimeOffset? _createdAt;
        private DateTimeOffset? _expiresAt;

        public string GenerateToken(TimeSpan duration)
        {
            _token = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            _createdAt = DateTimeOffset.UtcNow;
            _expiresAt = _createdAt.Value.Add(duration);

            return _token;
        }

        public string? Token => _token;

        public DateTimeOffset? CreatedAt => _createdAt;

        public DateTimeOffset? ExpiresAt => _expiresAt;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(_token)
                   && _createdAt.HasValue
                   && _expiresAt.HasValue
                   && DateTimeOffset.UtcNow < _expiresAt.Value;
        }

        public bool IsValid(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            if (!IsValid())
                return false;

            return string.Equals(
                token,
                _token,
                StringComparison.Ordinal);
        }

        public bool IsSessionValid(DateTimeOffset sessionCreatedAt)
        {
            if (!IsValid())
                return false;

            return sessionCreatedAt == _createdAt;
        }
    }
}

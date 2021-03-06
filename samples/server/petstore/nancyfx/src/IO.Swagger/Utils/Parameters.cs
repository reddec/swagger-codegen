using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Nancy;
using NodaTime;
using NodaTime.Text;
using Sharpility.Base;
using Sharpility.Extensions;
using Sharpility.Util;

namespace IO.Swagger.v2.Utils
{
    internal static class Parameters
    {
        private static readonly IDictionary<Type, Func<Parameter, object>> Parsers = CreateParsers();

        internal static TValue ValueOf<TValue>(dynamic parameters, Request request, string name, ParameterType parameterType)
        {
            var valueType = typeof(TValue);
            var valueUnderlyingType = Nullable.GetUnderlyingType(valueType);
            var isNullable = default(TValue) == null;
            string value = RawValueOf(parameters, request, name, parameterType);
            Preconditions.Evaluate(!string.IsNullOrEmpty(value) || isNullable, string.Format("Required parameter: '{0}' is missing", name));
            if (value == null && isNullable)
            {
                return default(TValue);
            }
            if (valueType.IsEnum || (valueUnderlyingType != null && valueUnderlyingType.IsEnum))
            {
                return EnumValueOf<TValue>(name, value);
            }
            return ValueOf<TValue>(parameters, name, value, valueType, request, parameterType);
        }

        private static string RawValueOf(dynamic parameters, Request request, string name, ParameterType parameterType)
        {
            try
            {
                switch (parameterType)
                {
                    case ParameterType.Query:
                        string querValue = request.Query[name];
                        return querValue;
                    case ParameterType.Path:
                        string pathValue = parameters[name];
                        return pathValue;
                    case ParameterType.Header:
                        var headerValue = request.Headers[name];
                        return headerValue != null ? string.Join(",", headerValue) : null;
                }
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(string.Format("Could not obtain value of '{0}' parameter", name), e);
            }
            throw new InvalidOperationException(string.Format("Parameter with type: {0} is not supported", parameterType));
        }

        private static TValue EnumValueOf<TValue>(string name, string value)
        {
            var valueType = typeof(TValue);
            var enumType = valueType.IsEnum ? valueType : Nullable.GetUnderlyingType(valueType);
            Preconditions.IsNotNull(enumType, () => new InvalidOperationException(
                string.Format("Could not parse parameter: '{0}' to enum. Type {1} is not enum", name, valueType)));
            var values = Enum.GetValues(enumType);
            foreach (var entry in values)
            {
                if (entry.ToString().EqualsIgnoreCases(value)
                    || ((int)entry).ToString().EqualsIgnoreCases(value))
                {
                    return (TValue)entry;
                }
            }
            throw new ArgumentException(string.Format("Parameter: '{0}' value: '{1}' is not supported. Expected one of: {2}",
                    name, value, Strings.ToString(values)));
        }

        private static TValue ValueOf<TValue>(dynamic parameters, string name, string value, Type valueType, Request request, ParameterType parameterType)
        {
            var parser = Parsers.GetIfPresent(valueType);
            if (parser != null)
            {
                return ParseValueUsing<TValue>(name, value, valueType, parser);
            }
            if (parameterType == ParameterType.Path)
            {
                return DynamicValueOf<TValue>(parameters, name);
            }
            if (parameterType == ParameterType.Query)
            {
                return DynamicValueOf<TValue>(request.Query, name);
            }
            throw new InvalidOperationException(string.Format("Could not get value for {0} with type {1}", name, valueType));
        }

        private static TValue ParseValueUsing<TValue>(string name, string value, Type valueType, Func<Parameter, object> parser)
        {
            var result = parser(Parameter.Of(name, value));
            try
            {
                return (TValue)result;
            }
            catch (InvalidCastException)
            {
                throw new InvalidOperationException(
                    string.Format("Could not parse parameter: '{0}' with value: '{1}'. " +
                                  "Received: '{2}', expected: '{3}'.",
                                  name, value, result.GetType(), valueType));
            }
        }

        private static TValue DynamicValueOf<TValue>(dynamic parameters, string name)
        {
            string value = parameters[name];
            try
            {
                TValue result = parameters[name];
                return result;
            }
            catch (InvalidCastException)
            {
                throw new InvalidOperationException(Strings.Format("Parameter: '{0}' value: '{1}' could not be parsed. " +
                                                                  "Expected type: '{2}' is not supported",
                                                                  name, value, typeof(TValue)));
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(string.Format("Could not get '{0}' value of '{1}' type dynamicly",
                    name, typeof(TValue)), e);
            }
        }

        private static IDictionary<Type, Func<Parameter, object>> CreateParsers()
        {
            var parsers = ImmutableDictionary.CreateBuilder<Type, Func<Parameter, object>>();
            parsers.Put(typeof(string), value => value.Value);
            parsers.Put(typeof(bool), SafeParse(bool.Parse));
            parsers.Put(typeof(bool?), SafeParse(bool.Parse));
            parsers.Put(typeof(byte), SafeParse(byte.Parse));
            parsers.Put(typeof(sbyte?), SafeParse(sbyte.Parse));
            parsers.Put(typeof(short), SafeParse(short.Parse));
            parsers.Put(typeof(short?), SafeParse(short.Parse));
            parsers.Put(typeof(ushort), SafeParse(ushort.Parse));
            parsers.Put(typeof(ushort?), SafeParse(ushort.Parse));
            parsers.Put(typeof(int), SafeParse(int.Parse));
            parsers.Put(typeof(int?), SafeParse(int.Parse));
            parsers.Put(typeof(uint), SafeParse(uint.Parse));
            parsers.Put(typeof(uint?), SafeParse(uint.Parse));
            parsers.Put(typeof(long), SafeParse(long.Parse));
            parsers.Put(typeof(long?), SafeParse(long.Parse));
            parsers.Put(typeof(ulong), SafeParse(ulong.Parse));
            parsers.Put(typeof(ulong?), SafeParse(ulong.Parse));
            parsers.Put(typeof(float), SafeParse(float.Parse));
            parsers.Put(typeof(float?), SafeParse(float.Parse));
            parsers.Put(typeof(double), SafeParse(double.Parse));
            parsers.Put(typeof(double?), SafeParse(double.Parse));
            parsers.Put(typeof(decimal), SafeParse(decimal.Parse));
            parsers.Put(typeof(decimal?), SafeParse(decimal.Parse));
            parsers.Put(typeof(DateTime), SafeParse(DateTime.Parse));
            parsers.Put(typeof(DateTime?), SafeParse(DateTime.Parse));
            parsers.Put(typeof(TimeSpan), SafeParse(TimeSpan.Parse));
            parsers.Put(typeof(TimeSpan?), SafeParse(TimeSpan.Parse));
            parsers.Put(typeof(ZonedDateTime), SafeParse(ParseZonedDateTime));
            parsers.Put(typeof(ZonedDateTime?), SafeParse(ParseZonedDateTime));
            parsers.Put(typeof(LocalTime), SafeParse(ParseLocalTime));
            parsers.Put(typeof(LocalTime?), SafeParse(ParseLocalTime));

            parsers.Put(typeof(IEnumerable<string>), value => value);
            parsers.Put(typeof(ICollection<string>), value => value);
            parsers.Put(typeof(IList<string>), value => value);
            parsers.Put(typeof(List<string>), value => value);
            parsers.Put(typeof(ISet<string>), value => value);
            parsers.Put(typeof(HashSet<string>), value => value);

            parsers.Put(typeof(IEnumerable<bool>), ImmutableListParse(bool.Parse));
            parsers.Put(typeof(ICollection<bool>), ImmutableListParse(bool.Parse));
            parsers.Put(typeof(IList<bool>), ImmutableListParse(bool.Parse));
            parsers.Put(typeof(List<bool>), ListParse(bool.Parse));
            parsers.Put(typeof(ISet<bool>), ImmutableSetParse(bool.Parse));
            parsers.Put(typeof(HashSet<bool>), SetParse(bool.Parse));

            parsers.Put(typeof(IEnumerable<byte>), ImmutableListParse(byte.Parse));
            parsers.Put(typeof(ICollection<byte>), ImmutableListParse(byte.Parse));
            parsers.Put(typeof(IList<byte>), ImmutableListParse(byte.Parse));
            parsers.Put(typeof(List<byte>), ListParse(byte.Parse));
            parsers.Put(typeof(ISet<byte>), ImmutableSetParse(byte.Parse));
            parsers.Put(typeof(HashSet<byte>), SetParse(byte.Parse));
            parsers.Put(typeof(IEnumerable<sbyte>), ImmutableListParse(sbyte.Parse));
            parsers.Put(typeof(ICollection<sbyte>), ImmutableListParse(sbyte.Parse));
            parsers.Put(typeof(IList<sbyte>), ImmutableListParse(sbyte.Parse));
            parsers.Put(typeof(List<sbyte>), ListParse(sbyte.Parse));
            parsers.Put(typeof(ISet<sbyte>), ImmutableSetParse(sbyte.Parse));
            parsers.Put(typeof(HashSet<sbyte>), SetParse(sbyte.Parse));

            parsers.Put(typeof(IEnumerable<short>), ImmutableListParse(short.Parse));
            parsers.Put(typeof(ICollection<short>), ImmutableListParse(short.Parse));
            parsers.Put(typeof(IList<short>), ImmutableListParse(short.Parse));
            parsers.Put(typeof(List<short>), ListParse(short.Parse));
            parsers.Put(typeof(ISet<short>), ImmutableSetParse(short.Parse));
            parsers.Put(typeof(HashSet<short>), SetParse(short.Parse));
            parsers.Put(typeof(IEnumerable<ushort>), ImmutableListParse(ushort.Parse));
            parsers.Put(typeof(ICollection<ushort>), ImmutableListParse(ushort.Parse));
            parsers.Put(typeof(IList<ushort>), ImmutableListParse(ushort.Parse));
            parsers.Put(typeof(List<ushort>), ListParse(ushort.Parse));
            parsers.Put(typeof(ISet<ushort>), ImmutableSetParse(ushort.Parse));
            parsers.Put(typeof(HashSet<ushort>), SetParse(ushort.Parse));

            parsers.Put(typeof(IEnumerable<int>), ImmutableListParse(int.Parse));
            parsers.Put(typeof(ICollection<int>), ImmutableListParse(int.Parse));
            parsers.Put(typeof(IList<int>), ImmutableListParse(int.Parse));
            parsers.Put(typeof(List<int>), ListParse(int.Parse));
            parsers.Put(typeof(ISet<int>), ImmutableSetParse(int.Parse));
            parsers.Put(typeof(HashSet<int>), SetParse(int.Parse));
            parsers.Put(typeof(IEnumerable<uint>), ImmutableListParse(uint.Parse));
            parsers.Put(typeof(ICollection<uint>), ImmutableListParse(uint.Parse));
            parsers.Put(typeof(IList<uint>), ImmutableListParse(uint.Parse));
            parsers.Put(typeof(List<uint>), ListParse(uint.Parse));
            parsers.Put(typeof(ISet<uint>), ImmutableSetParse(uint.Parse));
            parsers.Put(typeof(HashSet<uint>), SetParse(uint.Parse));

            parsers.Put(typeof(IEnumerable<long>), ImmutableListParse(long.Parse));
            parsers.Put(typeof(ICollection<long>), ImmutableListParse(long.Parse));
            parsers.Put(typeof(IList<long>), ImmutableListParse(long.Parse));
            parsers.Put(typeof(List<long>), ListParse(long.Parse));
            parsers.Put(typeof(ISet<long>), ImmutableSetParse(long.Parse));
            parsers.Put(typeof(HashSet<long>), SetParse(long.Parse));
            parsers.Put(typeof(IEnumerable<ulong>), ImmutableListParse(ulong.Parse));
            parsers.Put(typeof(ICollection<ulong>), ImmutableListParse(ulong.Parse));
            parsers.Put(typeof(IList<ulong>), ImmutableListParse(ulong.Parse));
            parsers.Put(typeof(List<ulong>), ListParse(ulong.Parse));
            parsers.Put(typeof(ISet<ulong>), ImmutableSetParse(ulong.Parse));
            parsers.Put(typeof(HashSet<ulong>), SetParse(ulong.Parse));

            parsers.Put(typeof(IEnumerable<float>), ImmutableListParse(float.Parse));
            parsers.Put(typeof(ICollection<float>), ImmutableListParse(float.Parse));
            parsers.Put(typeof(IList<float>), ImmutableListParse(float.Parse));
            parsers.Put(typeof(List<float>), ListParse(float.Parse));
            parsers.Put(typeof(ISet<float>), ImmutableSetParse(float.Parse));
            parsers.Put(typeof(HashSet<float>), SetParse(float.Parse));

            parsers.Put(typeof(IEnumerable<double>), ImmutableListParse(double.Parse));
            parsers.Put(typeof(ICollection<double>), ImmutableListParse(double.Parse));
            parsers.Put(typeof(IList<double>), ImmutableListParse(double.Parse));
            parsers.Put(typeof(List<double>), ListParse(double.Parse));
            parsers.Put(typeof(ISet<double>), ImmutableSetParse(double.Parse));
            parsers.Put(typeof(HashSet<double>), SetParse(double.Parse));

            parsers.Put(typeof(IEnumerable<decimal>), ImmutableListParse(decimal.Parse));
            parsers.Put(typeof(ICollection<decimal>), ImmutableListParse(decimal.Parse));
            parsers.Put(typeof(IList<decimal>), ImmutableListParse(decimal.Parse));
            parsers.Put(typeof(List<decimal>), ListParse(decimal.Parse));
            parsers.Put(typeof(ISet<decimal>), ImmutableSetParse(decimal.Parse));
            parsers.Put(typeof(HashSet<decimal>), SetParse(decimal.Parse));


            parsers.Put(typeof(IEnumerable<DateTime>), ImmutableListParse(DateTime.Parse));
            parsers.Put(typeof(ICollection<DateTime>), ImmutableListParse(DateTime.Parse));
            parsers.Put(typeof(IList<DateTime>), ImmutableListParse(DateTime.Parse));
            parsers.Put(typeof(List<DateTime>), ListParse(DateTime.Parse));
            parsers.Put(typeof(ISet<DateTime>), ImmutableSetParse(DateTime.Parse));
            parsers.Put(typeof(HashSet<DateTime>), SetParse(DateTime.Parse));

            parsers.Put(typeof(IEnumerable<TimeSpan>), ImmutableListParse(TimeSpan.Parse));
            parsers.Put(typeof(ICollection<TimeSpan>), ImmutableListParse(TimeSpan.Parse));
            parsers.Put(typeof(IList<TimeSpan>), ImmutableListParse(TimeSpan.Parse));
            parsers.Put(typeof(List<TimeSpan>), ListParse(TimeSpan.Parse));
            parsers.Put(typeof(ISet<TimeSpan>), ImmutableSetParse(TimeSpan.Parse));
            parsers.Put(typeof(HashSet<TimeSpan>), SetParse(TimeSpan.Parse));

            return parsers.ToImmutableDictionary();
        }

        private static Func<Parameter, object> SafeParse<T>(Func<string, T> parse)
        {
            return parameter =>
            {
                try
                {
                    return parse(parameter.Value);
                }
                catch (OverflowException)
                {
                    throw ParameterOutOfRange(parameter, typeof(T));
                }
                catch (FormatException)
                {
                    throw InvalidParameterFormat(parameter, typeof(T));
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(Strings.Format("Unable to parse parameter: '{0}' with value: '{1}' to {2}",
                        parameter.Name, parameter.Value, typeof(T)), e);
                }
            };
        }

        private static Func<Parameter, object> ListParse<T>(Func<string, T> itemParser)
        {
            return parameter =>
            {
                if (string.IsNullOrEmpty(parameter.Value))
                {
                    return new List<T>();
                }
                var results = parameter.Value.Split(new[] { ',' }, StringSplitOptions.None)
                    .Where(it => it != null)
                    .Select(it => it.Trim())
                    .Select(itemParser)
                    .ToList();
                return results;
            };
        }

        private static Func<Parameter, object> ImmutableListParse<T>(Func<string, T> itemParser)
        {
            return parameter =>
            {
                if (string.IsNullOrEmpty(parameter.Value))
                {
                    return Lists.EmptyList<T>();
                }
                var results = parameter.Value.Split(new[] { ',' }, StringSplitOptions.None)
                    .Where(it => it != null)
                    .Select(it => it.Trim())
                    .Select(itemParser)
                    .ToImmutableList();
                return results;
            };
        }

        private static Func<Parameter, object> SetParse<T>(Func<string, T> itemParser)
        {
            return parameter =>
            {
                if (string.IsNullOrEmpty(parameter.Value))
                {
                    return new HashSet<T>();
                }
                var results = parameter.Value.Split(new[] { ',' }, StringSplitOptions.None)
                    .Where(it => it != null)
                    .Select(it => it.Trim())
                    .Select(itemParser)
                    .ToSet();
                return results;
            };
        }

        private static Func<Parameter, object> ImmutableSetParse<T>(Func<string, T> itemParser)
        {
            return parameter =>
            {
                if (string.IsNullOrEmpty(parameter.Value))
                {
                    return Sets.EmptySet<T>();
                }
                var results = parameter.Value.Split(new[] { ',' }, StringSplitOptions.None)
                    .Where(it => it != null)
                    .Select(it => it.Trim())
                    .Select(itemParser)
                    .ToImmutableHashSet();
                return results;
            };
        }

        private static ZonedDateTime ParseZonedDateTime(string value)
        {
            var dateTime = DateTime.Parse(value);
            return new ZonedDateTime(Instant.FromDateTimeUtc(dateTime.ToUniversalTime()), DateTimeZone.Utc);
        }

        private static LocalTime ParseLocalTime(string value)
        {
            return LocalTimePattern.ExtendedIsoPattern.Parse(value).Value;
        }

        private static ArgumentException ParameterOutOfRange(Parameter parameter, Type type)
        {
            return new ArgumentException(Strings.Format("Query: '{0}' value: '{1}' is out of range for: '{2}'",
                parameter.Name, parameter.Value, type));
        }

        private static ArgumentException InvalidParameterFormat(Parameter parameter, Type type)
        {
            return new ArgumentException(Strings.Format("Query '{0}' value: '{1}' format is invalid for: '{2}'",
                parameter.Name, parameter.Value, type));
        }

        private class Parameter
        {
            internal string Name { get; private set; }
            internal string Value { get; private set; }

            private Parameter(string name, string value)
            {
                Name = name;
                Value = value;
            }

            internal static Parameter Of(string name, string value)
            {
                return new Parameter(name, value);
            }
        }
    }

    internal enum ParameterType
    {
        Undefined,
        Query,
        Path,
        Header
    }
}

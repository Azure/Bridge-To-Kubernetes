// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.RoutingManager.Envoy
{
    using System.Collections.Generic;
    using YamlDotNet.Serialization;

    internal partial class EnvoyConfig
    {
        [YamlMember(Alias = "static_resources", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public StaticResources StaticResources { get; set; }

        [YamlMember(Alias = "admin", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Admin Admin { get; set; }
    }

    internal partial class Admin
    {
        [YamlMember(Alias = "access_log_path", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string AccessLogPath { get; set; }
    }

    internal partial class StaticResources
    {
        [YamlMember(Alias = "listeners", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<Listener> Listeners { get; set; }

        [YamlMember(Alias = "clusters", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<Cluster> Clusters { get; set; }
    }

    internal partial class Cluster
    {
        [YamlMember(Alias = "name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Name { get; set; }

        [YamlMember(Alias = "connect_timeout", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string ConnectTimeout { get; set; }

        [YamlMember(Alias = "type", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Type { get; set; }

        [YamlMember(Alias = "load_assignment", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public LoadAssignment LoadAssignment { get; set; }

        [YamlMember(Alias = "http2_protocol_options", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public object Http2ProtocolOptions { get; set; }

        [YamlMember(Alias = "http_protocol_options", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public object Http1ProtocolOptions { get; set; }
    }

    internal partial class LoadAssignment
    {
        [YamlMember(Alias = "cluster_name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string ClusterName { get; set; }

        [YamlMember(Alias = "endpoints", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<EndpointElement> Endpoints { get; set; }
    }

    internal partial class EndpointElement
    {
        [YamlMember(Alias = "lb_endpoints", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<LbEndpoint> LbEndpoints { get; set; }
    }

    internal partial class LbEndpoint
    {
        [YamlMember(Alias = "endpoint", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public LbEndpointEndpoint Endpoint { get; set; }
    }

    internal partial class LbEndpointEndpoint
    {
        [YamlMember(Alias = "address", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public EndpointAddress Address { get; set; }
    }

    internal partial class EndpointAddress
    {
        [YamlMember(Alias = "socket_address", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public SocketAddress SocketAddress { get; set; }
    }

    internal partial class SocketAddress
    {
        [YamlMember(Alias = "address", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Address { get; set; }

        [YamlMember(Alias = "port_value", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public long PortValue { get; set; }
    }

    internal partial class Listener
    {
        [YamlMember(Alias = "name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Name { get; set; }

        [YamlMember(Alias = "address", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public ListenerAddress Address { get; set; }

        [YamlMember(Alias = "listener_filters", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<ListenerFilter> ListenerFilters { get; set; }

        [YamlMember(Alias = "filter_chains", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<FilterChain> FilterChains { get; set; }
    }

    internal partial class ListenerAddress
    {
        [YamlMember(Alias = "socket_address", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public SocketAddress SocketAddress { get; set; }
    }

    internal partial class ListenerFilter
    {
        [YamlMember(Alias = "name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Name { get; set; }

        [YamlMember(Alias = "typed_config", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public TypedConfig TypedConfig { get; set; }
    }

    internal partial class FilterChain
    {
        [YamlMember(Alias = "filter_chain_match", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public FilterChainMatch FilterChainMatch { get; set; }

        [YamlMember(Alias = "filters", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<Filter> Filters { get; set; }
    }

    internal partial class FilterChainMatch
    {
        [YamlMember(Alias = "application_protocols", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<string> ApplicationProtocols { get; set; }

        [YamlMember(Alias = "transport_protocol", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string TransportProtocol { get; set; }
    }

    internal partial class Filter
    {
        [YamlMember(Alias = "name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Name { get; set; }

        [YamlMember(Alias = "typed_config", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public TypedConfig TypedConfig { get; set; }

        [YamlMember(Alias = "config", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Filter_Config Config { get; set; }
    }

    internal partial class Filter_Config
    {
        [YamlMember(Alias = "stat_prefix", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Stat_Prefix { get; set; }

        [YamlMember(Alias = "cluster", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Cluster { get; set; }
    }

    internal partial class TypedConfig
    {
        [YamlMember(Alias = "@type", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Type { get; set; }

        [YamlMember(Alias = "codec_type", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string CodecType { get; set; }

        [YamlMember(Alias = "stat_prefix", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string StatPrefix { get; set; }

        [YamlMember(Alias = "route_config", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public RouteConfig RouteConfig { get; set; }

        [YamlMember(Alias = "http_filters")]
        public List<HttpFilter> HttpFilters { get; set; }
    }

    internal partial class RouteConfig
    {
        [YamlMember(Alias = "name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Name { get; set; }

        [YamlMember(Alias = "virtual_hosts", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<VirtualHost> VirtualHosts { get; set; }
    }

    internal partial class HttpFilter
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; }
    }

    internal partial class VirtualHost
    {
        [YamlMember(Alias = "name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Name { get; set; }

        [YamlMember(Alias = "domains", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<string> Domains { get; set; }

        [YamlMember(Alias = "routes", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<RouteElement> Routes { get; set; }
    }

    internal partial class RouteElement
    {
        [YamlMember(Alias = "match", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Match Match { get; set; }

        [YamlMember(Alias = "route", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Route Route { get; set; }

        [YamlMember(Alias = "request_headers_to_add", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<RequestHeadersToAdd> RequestHeadersToAdd { get; set; }
    }

    internal partial class Match
    {
        [YamlMember(Alias = "headers", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<Header> Headers { get; set; }

        [YamlMember(Alias = "prefix", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Prefix { get; set; }
    }

    internal partial class Header
    {
        [YamlMember(Alias = "name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Name { get; set; }

        [YamlMember(Alias = "exact_match", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Value { get; set; }
    }

    internal partial class Route
    {
        [YamlMember(Alias = "cluster", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Cluster { get; set; }

        // https://www.envoyproxy.io/docs/envoy/latest/api-v3/config/route/v3/route_components.proto#envoy-v3-api-field-config-route-v3-routeaction-timeout
        [YamlMember(Alias = "timeout", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Timeout { get; set; } = "0s";

        // https://www.envoyproxy.io/docs/envoy/latest/api-v3/config/route/v3/route_components.proto#envoy-v3-api-field-config-route-v3-routeaction-idle-timeout
        [YamlMember(Alias = "idle_timeout", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string IdleTimeout { get; set; } = "0s";
        
        [YamlMember(Alias = "auto_host_rewrite", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public bool AutoHostRewrite { get; set; }
    }

    internal partial class RequestHeadersToAdd
    {
        [YamlMember(Alias = "header", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public RequestHeaderToAdd Header { get; set; }

        [YamlMember(Alias = "append", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public bool Append { get; set; }
    }

    internal partial class RequestHeaderToAdd
    {
        [YamlMember(Alias = "key", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Key { get; set; }

        [YamlMember(Alias = "value", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string Value { get; set; }
    }
}
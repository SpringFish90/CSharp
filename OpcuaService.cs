using Opc.Ua;
using Opc.Ua.Client;
using StatusCodes = Opc.Ua.StatusCodes;

public class OpcuaService : BackgroundService
    {
        private static readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();
        // 임시 urls
        private static readonly string[] _endpointUrls = new string[]
        {
            "opc.tcp://128.128.32.1:49330",
            "opc.tcp://128.128.32.2:49330"
        };
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 모든 엔드포인트에 대해 초기 연결 시도
            foreach (var url in _endpointUrls)
            {
                _ = Task.Run(() => KeepSessionAlive(url, stoppingToken), stoppingToken);
            }

            // 서비스는 cancellation 요청 올 때까지 유지
            await Task.인
                    while (_sessions.TryGetValue(endpointUrl, out var session) && session.Connected && !stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, stoppingToken);
                    }

                    Console.WriteLine($"X 세션 끊김 감지됨: {endpointUrl}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"X 세션 유지 중 예외 발생 ({endpointUrl}): {ex.Message}");
                }

                // 재시도 전 5초 대기
                await Task.Delay(5000, stoppingToken);
            }
        }
        private static async Task tryConnection(string endpointUrl)
        {
            if (_sessions.TryGetValue(endpointUrl, out var existing) && existing.Connected)
            {
                Console.WriteLine($"O 서버 ({endpointUrl}) 이미 연결됨");
                return;
            }

            var config = new ApplicationConfiguration
            {
                ApplicationName = "OpcUaClient",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "Directory",
                        StorePath = "CertificateStores/UA_MachineDefault",
                        SubjectName = "CN=MyOpcUaClient"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = "CertificateStores/UA_TrustedIssuer" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = "CertificateStores/UA_TrustedPeers" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = "CertificateStores/UA_Rejected" },
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    AddAppCertToTrustedStore = true
                },
                TransportQuotas = new TransportQuotas { OperationTimeout = 10000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };
            await config.Validate(ApplicationType.Client);

            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
            var endpointConfig = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

            var session = await Session.Create(config, endpoint, false, "MyReadWriteSession", 60000, null, null);

            // 기존 세션 교체
            if (existing != null)
            {
                try { existing.Close(); existing.Dispose(); } catch { }
            }

            _sessions[endpointUrl] = session;
            Console.WriteLine($"O 서버 ({endpointUrl}) 연결 성공");

            // 세션 끊김 이벤트 핸들러
            session.KeepAlive += (s, e) =>
            {
                if (e.Status != null && ServiceResult.IsBad(e.Status))
                {
                    Console.WriteLine($"X KeepAlive 오류 ({endpointUrl}): {e.Status}");
                    session.Dispose();
                }
            };
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var session in _sessions.Values)
            {
                if (session != null && session.Connected)
                {
                    session.Close();
                    Console.WriteLine($"X 세션 종료: {session.Endpoint.EndpointUrl}");
                }
            }
            return base.StopAsync(cancellationToken);
        }
        private static async Task<(Session session, string msg)> GetSessionAsync(int sessionIndex)
        {
            string rtnMsg = "";
            if (sessionIndex < 0 || sessionIndex >= _endpointUrls.Length)
            {
                rtnMsg = $"X 세션 인덱스가 범위를 벗어났습니다. 인덱스: {sessionIndex}, 유효 범위: 0-{_endpointUrls.Length - 1}";
                return (null, rtnMsg);
            }

            var endpointUrl = _endpointUrls[sessionIndex];
            if (!_sessions.TryGetValue(endpointUrl, out var session) || !session.Connected)
            {try
                {
                    // 비동기 재연결 시도
                    await tryConnection(endpointUrl);

                    // 재연결 성공 후 새로운 세션 반환
                    if (_sessions.TryGetValue(endpointUrl, out var newSession) && newSession.Connected)
                    {
                        return (newSession, $"O 세션 재연결 성공: {endpointUrl}");
                    }
                    else
                    {
                        rtnMsg = $"X 세션 재연결 실패: {endpointUrl}";
                    }
                }
                catch (Exception ex)
                {
                    rtnMsg = $"X 세션 재연결 중 예외 발생 ({endpointUrl}): {ex.Message}";
                }
            }

            return (session, rtnMsg);
        }
        public static async Task<DataValue> ReadValueAsync(int sessionIndex, string nodeIdStr)
        {
            (Session session, string msg) = await GetSessionAsync(sessionIndex);
            Console.WriteLine(msg);
            if (session == null)
            {
                return null;
            }

            try
            {
                var nodeId = new NodeId(nodeIdStr, 2);
                var readValue = new ReadValueId
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value
                };
                var response = await session.ReadAsync(
                    requestHeader: null,
                    maxAge: 0,
                    timestampsToReturn: TimestampsToReturn.Neither,
                    nodesToRead: new ReadValueIdCollection { readValue },
                    CancellationToken.None);
                var results = response.Results[0];
                if (StatusCode.IsGood(results.StatusCode))
                {
                    Console.WriteLine($"O 노드 ({nodeIdStr})의 현재 값: {results.Value}");
                    return results;
                }
                else
                {
                    Console.WriteLine($"X 노드 ({nodeIdStr}) 값 읽기 실패: {results.StatusCode}");
                    return results;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"X 값 읽기 중 예외 발생: {ex.Message}");
                return new DataValue(StatusCodes.BadUnexpectedError);
            }
        }
        public static async Task<DataValueCollection> ReadValueAsync(int sessionIndex, string[] nodeIdStrList)
        {
            (Session session, string msg) = await GetSessionAsync(sessionIndex);
            Console.WriteLine(msg);
            if (session == null)
            {
                return null;
            }

            try
            {
                var nodesToRead = new ReadValueIdCollection();
                foreach (var nodeIdStr in nodeIdStrList)
                {
                    nodesToRead.Add(new ReadValueId
                    {
                        NodeId = new NodeId(nodeIdStr, 2),
                        AttributeId = Attributes.Value
                    });
                }

                // 여러 노드를 한 번의 요청으로 읽기
                var results = await session.ReadAsync(
                    requestHeader: null,
                    maxAge: 0,
                    timestampsToReturn: TimestampsToReturn.Neither,
                    nodesToRead: nodesToRead,
                    CancellationToken.None);

                return results.Results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"X 값 읽기 중 예외 발생: {ex.Message}");
                return null;
            }
        }
        public static async Task<StatusCode> WriteValueAsync(int sessionIndex, string nodeIdStr, object value)
        {
            (Session session, string msg) = await GetSessionAsync(sessionIndex);
            Console.WriteLine(msg);
            if (session == null)
            {
                throw new Exception(msg);
            }

            try
            {
                var nodeId = new NodeId(nodeIdStr, 2);
                var convertedValue = await ConvertValueToProperType(session, nodeId, value);

                var writeValue = new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(convertedValue))
                };
                var response = await session.WriteAsync(
                    requestHeader: null,
                    nodesToWrite: new WriteValueCollection { writeValue },
                    CancellationToken.None);
                var results = response.Results[0];
                if (StatusCode.IsGood(results))
                {
                    Console.WriteLine($"O 노드 ({nodeIdStr})에 값 '{convertedValue}' 쓰기 성공.");
                }
                else
                {
                    Console.WriteLine($"X 노드 ({nodeIdStr})에 값 쓰기 실패: {results}");
                    throw new Exception($"노드 ({nodeIdStr})에 값 쓰기 실패: {results}");
                }
                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"X 값 쓰기 중 예외 발생: {ex.Message}");
                throw new Exception($"X 값 쓰기 중 예외 발생: {ex.Message}");
            }
        }
        public static async Task<StatusCode[]> WriteValueAsync(int sessionIndex, Dictionary<string, object> values)
        {
            (Session session, string msg) = await GetSessionAsync(sessionIndex);
            Console.WriteLine(msg);
            if (session == null)
            {
                throw new Exception(msg);
            }

            try
            {
                var writeValues = new WriteValueCollection();

                foreach (var kvp in values)
                {
                    var nodeIdStr = kvp.Key;
                    var value = kvp.Value;

                    var nodeId = new NodeId(nodeIdStr, 2); // namespace index 필요 시 조정
                    var convertedValue = await ConvertValueToProperType(session, nodeId, value);

                    var writeValue = new WriteValue
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value,
                        Value = new DataValue(new Variant(convertedValue))
                    };

                    writeValues.Add(writeValue);
                }

                var response = await session.WriteAsync(
                    requestHeader: null,
                    nodesToWrite: writeValues,
                    CancellationToken.None);

                var results = response.Results;

                for (int i = 0; i < results.Count; i++)
                {
                    var status = results[i];
                    var nodeIdStr = values.Keys.ElementAt(i);
                    var value = values.Values.ElementAt(i);

                    if (StatusCode.IsGood(status))
                    {
                        Console.WriteLine($"O 노드 ({nodeIdStr})에 값 '{value}' 쓰기 성공.");
                    }
                    else
                    {
                        Console.WriteLine($"X 노드 ({nodeIdStr})에 값 쓰기 실패: {status}");
                        throw new Exception($"노드 ({nodeIdStr})에 값 쓰기 실패: {results}");
                    }
                }

                return results.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"X 다수 값 쓰기 중 예외 발생: {ex.Message}");
                throw new Exception($"X 값 쓰기 중 예외 발생: {ex.Message}");
            }
        }
        private static async Task<object> ConvertValueToProperType(Session session, NodeId nodeId, object value)
        {
            object convertedValue = value;

            try
            {
                var node = await session.ReadNodeAsync(nodeId);
                var dataTypeId = ((Opc.Ua.VariableNode)node).DataType.Identifier as uint?;

                if (dataTypeId.HasValue)
                {
                    switch (dataTypeId.Value)
                    {
                        case 1: // Boolean
                            convertedValue = Convert.ToBoolean(value);
                            break;
                        case 2: // SByte
                            convertedValue = Convert.ToSByte(value);
                            break;
                        case 3: // Byte
                            convertedValue = Convert.ToByte(value);
                            break;
                        case 4: // Int16
                            convertedValue = Convert.ToInt16(value);
                            break;
                        case 5: // UInt16
                            convertedValue = Convert.ToUInt16(value);
                            break;
                        case 6: // Int32
                            convertedValue = Convert.ToInt32(value);
                            break;
                        case 7: // UInt32
                            convertedValue = Convert.ToUInt32(value);
                            break;
                        case 8: // Int64
                            convertedValue = Convert.ToInt64(value);
                            break;
                        case 9: // UInt64
                            convertedValue = Convert.ToUInt64(value);
                            break;
                        case 10: // Float
                            convertedValue = Convert.ToSingle(value);
                            break;
                        case 11: // Double
                            convertedValue = Convert.ToDouble(value);
                            break;
                        case 12: // String
                            convertedValue = Convert.ToString(value);
                            break;
                        default:
                            Console.WriteLine($"X 알 수 없는 DataType: i={dataTypeId.Value}, 그대로 사용");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"X 노드({nodeId})의 DataType을 읽는 중 예외 발생: {ex.Message}");
                throw new Exception($"X 노드({nodeId})의 DataType을 읽는 중 예외 발생: {ex.Message}");
            }

            return convertedValue;
        }
    }

const grpc = require('grpc')
const request = require('request')
const externalScalerProto = grpc.load('externalscaler.proto')

const server = new grpc.Server()
server.addService(externalScalerProto.externalscaler.ExternalScaler.service, {
    isActive: (call, callback) => {
        const longitude = call.request.scalerMetadata.longitude
        const latitude = call.request.scalerMetadata.latitude
        if (!longitude || !latitude) {
            callback({
                code: grpc.status.INVALID_ARGUMENT,
                details: 'longitude and latitude must be specified',
            })
        } else {
            const now = new Date()
            const yesterday = new Date(new Date().setDate(new Date().getDate() - 1));

            const startTime = `${yesterday.getUTCFullYear()}-${yesterday.getUTCMonth()}-${yesterday.getUTCDay()}`
            const endTime = `${now.getUTCFullYear()}-${now.getUTCMonth()}-${now.getUTCDay()}`
            const radiusKm = 500
            const query = `format=geojson&starttime=${startTime}&endtime=${endTime}&longitude=${longitude}&latitude=${latitude}&maxradiuskm=${radiusKm}`

            request.get({
                url: `https://earthquake.usgs.gov/fdsnws/event/1/query?${query}`,
                json: true,
            }, (err, resp, data) => {
                if (err) {
                    callback({
                        code: grpc.status.INTERNAL,
                        details: err,
                    })
                } else if (resp.statusCode !== 200) {
                    callback({
                        code: grpc.status.INTERNAL,
                        details: `expected status 200, got ${resp.statusCode}`
                    })
                } else {
                    let count = 0
                    data.features.forEach(i => {
                        if (i.properties.mag > 1.0) {
                            count++
                        }
                    })
                    callback(null, {
                        result: count > 2,
                    })
                }
            })
        }
    },
    streamIsActive: (call, callback) => {
        const longitude = call.request.scalerMetadata.longitude
        const latitude = call.request.scalerMetadata.latitude
        if (!longitude || !latitude) {
            callback({
                code: grpc.status.INVALID_ARGUMENT,
                details: 'longitude and latitude must be specified',
            })
        } else {
            const interval = setInterval(() => {
                getEarthquackeCount((err, count) => {
                    if (err) {
                        console.error(err)
                    } else if (count > 2) {
                        call.write({
                            result: true,
                        })
                    }
                })
            }, 1000 * 60 * 60);

            call.on('end', () => {
                clearInterval(interval)
            })
        }
    },
    getMetricSpec: (call, callback) => {
        callback(null, {
            metricSpecs: [{
                metricName: 'earthquakeThreshold',
                targetSize: 10,
            }]
        })
    },
    getMetrics: (call, callback) => {
        const longitude = call.request.scaledObjectRef.scalerMetadata.longitude
        const latitude = call.request.scaledObjectRef.scalerMetadata.latitude
        if (!longitude || !latitude) {
            callback({
                code: grpc.status.INVALID_ARGUMENT,
                details: 'longitude and latitude must be specified',
            })
        } else {
            getEarthquackeCount((err, count) => {
                if (err) {
                    callback({
                        code: grpc.status.INTERNAL,
                        details: err,
                    })
                } else {
                    callback(null, {
                        metricValues: [{
                            metricName: 'earthquakeThreshold',
                            metricValue: count,
                        }]
                    })
                }
            })
        }
    }
})

function getEarthquackeCount(callback) {
    callback(null, 10)
}

server.bind('0.0.0.0:7000', grpc.ServerCredentials.createInsecure())

console.log('Server listening on 0.0.0.0:7000')

server.start()
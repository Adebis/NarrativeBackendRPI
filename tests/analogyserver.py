#! /usr/bin/env python3
from flask import Flask
from flask import request
from flask import Response
import urllib
import urllib.request
import urllib.parse
import json
import cgi

from analogyscoretest3_9a1 import AIMind

app = Flask(__name__)

@app.route('/get_analogy', methods=['GET', 'POST'])
def get_analogy():
    try:
        #use JSON if POST else check args for GET
        idata = request.get_json() if request.method == "POST" else request.args
        id_filter = idata.get("id_filter")
        port = idata.get("port")
        feature_id = idata.get("id")
        filename = idata.get("filename")
        target_id = idata.get("target_id")

        if not feature_id:
            return "Error: must specify a feature"
        if not port and not filename:
            return "Error: must supply callback port or filename"
        if port:
            return_address = "http://%s:%s"%(request.remote_addr, port)
            print("ret: ",return_address)
            #get graph data from knowledge explorer
            graphdata = urllib.request.urlopen("%s/generate/xml"%return_address)
            graphdata_buffer = graphdata.read()#.decode("utf-8")
            a1 = AIMind(rawdata=graphdata_buffer)

        if filename and not port:
            a1 = AIMind(filename=urllib.parse.unquote(filename) )

        if id_filter: #convert list of id to list of names
            id_filter = [a1.get_feature(fid) for fid in id_filter]


        if not target_id:
            analogyData = a1.find_best_analogy(a1.get_feature(feature_id), a1, id_filter)
        else:
            analogyData = a1.get_analogy(a1.get_feature(feature_id), a1.get_feature(target_id), a1)

        if analogyData:
            nrating, rating, total_rating, (src,trg), rassert, mapping = analogyData
            evidence = [(a1.get_id(a[1]),a1.get_id(b[1])) for a,b in mapping.items()]
            explanation = cgi.escape(a1.explain_analogy(analogyData))
            data = {
                "source":a1.get_id(src), #source topic
                "target":a1.get_id(trg), #target topic
                "evidence":evidence, #analogous mappings
                "connections":[], #direct connections
                "explanation":explanation, #text explanation,
                "n_rating":nrating,
                "rating":rating,

            }
        else:
            data = {}

        encoded_data = json.dumps(data).encode('utf8')

        if port:
            #post data back to knowledge explorer
            req = urllib.request.Request(
                            "%s/callback/analogy"%return_address,
                            data=encoded_data,
                            headers={'content-type': 'application/json'})
            urllib.request.urlopen(req)
            return "Success"

        else:
            #just return the analogy
            return encoded_data
    except Exception as e:
        return "Error: %r"%e



@app.route('/get_analogy_narration', methods=['GET'])
def get_analogy_narration():
    #try:
        feature_id = request.args["id"]
        filename = request.args["filename"]
        return_address = "http://%s"%request.remote_addr

        print("ret: ",return_address)

        if not filename:
            #get graph data from knowledge explorer
            graphdata = urllib.request.urlopen("%s/generate/xml"%return_address)
            graphdata_buffer = graphdata.read()#.decode("utf-8")
            a1 = AIMind(rawdata=graphdata_buffer)
        else:
            #else read specified file
            a1 = AIMind(filename)
        analogyData = a1.find_best_analogy(a1.get_feature(feature_id),a1)

        if analogyData:
            score, (src, trg), mapping, hypotheses = analogyData
            evidence = [(a1.get_id(a[1]),a1.get_id(b[1])) for a,b in hypotheses.items()]
            explanation = cgi.escape(a1.explain_analogy(analogyData))
            data = {
                "source":a1.get_id(src), #source topic
                "target":a1.get_id(trg), #target topic
                "evidence":evidence, #analogous mappings
                "connections":[], #direct connections
                "explanation":explanation #text explanation
            }
        else:
            data = {}

        #construct response
        print("source: " + data["source"])
        print("target: " + data["target"])
        data=json.dumps(data).encode('utf8')
        resp = Response(data,
                        mimetype='application/json')

        return resp

    #except Exception as e:
        return "Exception: ",e

if __name__ == '__main__':
    app.run(debug=True)

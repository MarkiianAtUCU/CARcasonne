import numpy as np
import cv2
import imutils
import os
import websockets
import asyncio
import json

template_folder = cv2.imread('/tiles/')
threshold = 0.6
rotations = [0, 90, 180, 270]


def non_max_suppression_fast(boxes, w, h, overlapThresh):
    if len(boxes) == 0:
        return []
    pick = []
    x1 = boxes[:,0]
    y1 = boxes[:,1]
    x2 = boxes[:,0] + w
    y2 = boxes[:,1] + h
    area = (x2 - x1 + 1) * (y2 - y1 + 1)
    idxs = np.argsort(y2)
    while len(idxs) > 0:
        last = len(idxs) - 1
        i = idxs[last]
        pick.append(i)
        xx1 = np.maximum(x1[i], x1[idxs[:last]])
        yy1 = np.maximum(y1[i], y1[idxs[:last]])
        xx2 = np.minimum(x2[i], x2[idxs[:last]])
        yy2 = np.minimum(y2[i], y2[idxs[:last]])
        w = np.maximum(0, xx2 - xx1 + 1)
        h = np.maximum(0, yy2 - yy1 + 1)
        overlap = (w * h) / area[idxs[:last]]
        idxs = np.delete(idxs, np.concatenate(([last],
            np.where(overlap > overlapThresh)[0])))
    return boxes[pick].astype("int")


async def process_image(new_data):
    data = []
    image = cv2.cvtColor(cv2.imdecode(np.frombuffer(new_data, np.uint8), -1)[200:700, 200:700], cv2.COLOR_RGB2GRAY)
    # image_copy = cv2.imdecode(np.frombuffer(new_data, np.uint8), -1)[200:700, 200:700]

    for file in os.listdir(os.path.join(os.getcwd(), "tiles")):
        template = cv2.cvtColor(cv2.imread(os.path.join(os.path.join(os.getcwd(), "tiles"), file)), cv2.COLOR_BGR2GRAY)
        for rotation in rotations:
            rotated = imutils.rotate(template, rotation)
            for scale in np.linspace(0.1, 0.25, 10)[::-1]:
                resized = imutils.resize(rotated, width=int(rotated.shape[1] * scale))
                w, h = resized.shape[::-1]
                res = cv2.matchTemplate(image, resized, cv2.TM_CCOEFF_NORMED)
                loc = np.where(res >= threshold)
                smth = non_max_suppression_fast(np.array(list(zip(*loc[::-1]))), w, h, 0.2)
                if len(smth) > 0:
                    temp = []
                    for sub_arr in smth:
                        for num in sub_arr:
                            temp.append(num + 200)
                    data.append(json.dumps({"points": temp, "rotation": rotation, "type": int(file.split("_")[-1].split(".")[0])}).encode("ASCII"))
                    # data.append([smth, w, h, rotation, file.split("_")[-1].split(".")[0]])
                    # data.append([smth+200, rotation, int(file.split("_")[-1].split(".")[0])])

    # for position in data:
    #     for pt in position[0]:
    #         cv2.rectangle(image_copy, (pt[0], pt[1]), (pt[0] + position[1], pt[1] + position[2]), (0, 0, 255), 1)
    #         cv2.putText(image_copy, f"{position[4]} | {position[3]} Deg", (pt[0], pt[1]), cv2.FONT_HERSHEY_SIMPLEX, 0.5,
    #                     (255, 255, 255), 1, cv2.LINE_AA)

    return data
    # cv2.imshow("A", image_copy)
    # cv2.waitKey(1)


async def hello(websocket, path):
    while 1:
        # message = await websocket.recv()
        # results = await process_image(message)
        test = [{"points": [1,2,3,4], "rotation": 180, "type": 2}, {"points": [5,6,7,8], "rotation": 270, "type": 67}]
        await websocket.send(json.dumps(test).encode("ASCII"))

start_server = websockets.serve(hello, "192.168.31.135", 5353)

asyncio.get_event_loop().run_until_complete(start_server)
asyncio.get_event_loop().run_forever()

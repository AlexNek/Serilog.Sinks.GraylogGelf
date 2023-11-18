# Graylog
## Overview
If you only have one desktop application, then it is not a bad idea to use the File Logger.
However, if you have a lot of applications on different computers, it will not be so easy to have a look at file-based logs for each application.
The better solution is to use a single location for all of your application logs.
One of the solutions is Graylog software, which centrally captures, stores and enables real-time search and log analysis.
You can use most standard loggers with extensions to send a log stream. Don't forget that each log message can have more than one additional property,
like - app_name="My_Great_Application". It will be easy to use these properties to search for messages.


## Run graylog locally
For testing purposes, you can simply run graylog locally, installed on docker with docker compose.
As long it is running, add GELF tcp and GELF udp inputs.

For windows installation and personal account you can do the next steps:

1. Install Docker desktop software and reboot computer.
2. Create work folder and write here file docker-compose.yml with this content
  
```
  version: '3'
  services:
# MongoDB: https://hub.docker.com/_/mongo/
    mongo:
      image: mongo:5.0.13
      networks:
        - graylog
# Elasticsearch: https://www.elastic.co/guide/en/elasticsearch/reference/7.10/docker.html
    elasticsearch:
      image: docker.elastic.co/elasticsearch/elasticsearch-oss:7.10.2
      environment:
        - http.host=0.0.0.0
        - transport.host=localhost
        - network.host=0.0.0.0
        - "ES_JAVA_OPTS=-Dlog4j2.formatMsgNoLookups=true -Xms512m -Xmx512m"
      ulimits: 
        memlock: 
         soft: -1
         hard: -1
      deploy:
        resources:
          limits:
            memory: 1g
      networks:
        - graylog
# Graylog: https://hub.docker.com/r/graylog/graylog/
    graylog:
      image: graylog/graylog:5.0
      environment:
# CHANGE ME (must be at least 16 characters)!
      - GRAYLOG_PASSWORD_SECRET=somepasswordpepper
# Password: admin
      - GRAYLOG_ROOT_PASSWORD_SHA2=8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918
      - GRAYLOG_HTTP_EXTERNAL_URI=http://127.0.0.1:9000/
      entrypoint: /usr/bin/tini -- wait-for-it elasticsearch:9200 --  /docker-entrypoint.sh
      networks:
        - graylog
      restart: always
      depends_on:
        - mongo
        - elasticsearch
      ports:
# Graylog web interface and REST API
      - 9000:9000
# Syslog TCP
      - 1514:1514
# Syslog UDP
      - 1514:1514/udp
# GELF TCP
      - 12201:12201
# GELF UDP
      - 12201:12201/udp
  networks:
    graylog:
      driver: bridge
```

3. Run command docker-compose up
4. Open url http://localhost:9000 and login with user `admin` and password `admin`
5. Setup inputs. We need GELF format.
![image](pics/gr_input-menu.jpg)
![image](pics/gr_input-details.jpg)

## General usage with C# application
I'd like to use serilog as a single logger provider, in which case you can use GraylogGelf sink.
It is possible to use Serilog as an additional logging provider for Microsoft logging, in this case you need to use `AddSerilog()`, not `UseSerilog()`.
To test, you can try running one of the Sample Applications. Then you will see something like this:

<figure>
  <img
  src="pics/gr_main.jpg"
  alt="Image.">
  <figcaption>pic.1</figcaption>
</figure>

## UI setup
On the search page (pic.1) you could find some areas:
1. Timeline settings
2. Query line
3. Bar chart - messages per time interval (not working for `All time`)
4. Messages table title
5. Message line

If you click on message line (5) you can see additional properties:
![Image](pics/gr_add-to-table.png)
If you click on title menu triangle you can add this property as table column.
If you click on text menu triangle you can add this text as query filter.

It is possible to edit messages table too
![Image](pics/gr_edit-table.png)
You can add or delete fields and change field order with drag&drop. It is possible to minimize table if we disable show messages in additional row
![Image](pics/gr_edit-table2.png)
It is possible to store/load table settings

### How to create a dashboard
If you want to see a bit more than a text table, try creating a dashboard.
![Image](pics/gr_dashboard-overview.png)
For start, export your table as dasboard.
![Image](pics/gr_export-dashboard.png)
Then you can add more dashboard parts and position them by dragging and dropping (top left corner with 3 little lines).
![Image](pics/gr_add_dashboard_part.png)
For pie chart you need to create `Group By` and `Metrics` for the same field.
![Image](pics/gr_add-pie-chart.png)
You can edit dashboard title too
![Image](pics/gr_edit_dashborad-title.png)


## Additional information

https://go2docs.graylog.org/5-0/downloading_and_installing_graylog/docker_installation.htm

If you want to build components itself, you can use this command:
docker pull docker.elastic.co/elasticsearch/elasticsearch:8.8.2
https://jinnabalu.medium.com/elasticsearch-on-docker-b7854f116062

https://hub.docker.com/r/opensearchproject/opensearch
docker pull opensearchproject/opensearch

How to find containers IPs: docker network inspect bridge


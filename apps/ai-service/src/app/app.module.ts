import { Module } from '@nestjs/common';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { ConfigModule } from './config/config.module';
import { HealthModule } from './health/health.module';
import { LoggingModule } from './logging/logging.module';
import { MessagingModule } from './messaging/messaging.module';
import { RagModule } from './rag/rag.module';
import { ClassificationModule } from './classification/classification.module';
import { SummariesModule } from './summaries/summaries.module';

@Module({
  imports: [
    ConfigModule,
    HealthModule,
    LoggingModule,
    MessagingModule,
    RagModule,
    ClassificationModule,
    SummariesModule,
  ],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
